using SQLiteNetExtensions.Extensions;

// TODO: Add file import/export
public static class Backup {
    const FileAttributes EXCLUDED_ATTRIBUTES =
        FileAttributes.Directory |
        FileAttributes.Hidden |
        FileAttributes.System |
        FileAttributes.Device |
        FileAttributes.Offline |
        FileAttributes.ReparsePoint
    ;

    const string ARCHIVE_FILE_SQL = "" +
        "UPDATE files SET archived = 1, restore_with_folder = ? " +
        "WHERE id = ? AND archived = 0"
    ;

    const string ARCHIVE_FOLDER_FOLDERS_SQL = "" +
        "UPDATE folders SET archived = 1 " +
        "WHERE id = ? AND archived = 0"
    ;

    const string ARCHIVE_FOLDER_FILES_SQL = "" +
        "UPDATE files SET " +
            "archived = 1, " +
            "restore_with_folder = 1 " +
        "WHERE folder_id = ? AND archived = 0"
    ;

    const int MAX_FILE_IDS_PER_QUERY = 998;

    public static void AddFolder(string path) {
        lock (Database.WriteLock) {
            if (!Directory.Exists(path)) {
                throw new Exception(
                    "Attempting to create a database record for a folder " +
                    $"that does not exist: {path}"
                );
            }

            Folder? existingFolder = Database.Connection
                .Table<Folder>()
                .Where(folder => !folder.Archived)
                .FirstOrDefault(folder => folder.Path == path)
            ;

            if (existingFolder != null) {
                throw new Exception(
                    "Attempting to create a duplicate database record for a " +
                    $"tracked folder: {path}"
                );
            }

            Folder folder = new() {
                Name = Path.GetFileName(path),
                Path = path,
            };

            IEnumerable<File> files = [];

            // Add to database
            try {
                Database.Connection.RunInTransaction(() => {
                    // Create record for folder
                    Database.Connection.Insert(folder);

                    // Create records for folder's current files
                    files = Directory.GetFiles(path)
                        .Where(path => IsTrackable(path))
                        .Select(path => new File {
                            FolderId = folder.Id,
                            Name = Path.GetFileName(path),
                            Path = path
                        })
                        .ToList()
                    ;
                    Database.Connection.InsertAll(files);
                });
            } catch (Exception e) {
                throw new Exception(
                    "There was an issue adding a new folder to the database.",
                    e
                );
            }

            // Initialize diff tracking
            foreach (File file in files) Diff.Init(file);

            // Start watcher
            FolderWatcherService.Watch(folder);
        }
    }

    public static void AddFile(string path) {
        lock (Database.WriteLock) {
            if (!System.IO.File.Exists(path)) {
                throw new Exception(
                    "Attempting to create a database record for a file that " +
                    $"does not exist: {path}"
                );
            }

            if (!IsTrackable(path)) {
                throw new Exception(
                    "Attempting to create a database record for an " +
                    $"untrackable file: {path}"
                );
            }

            string? folderPath = Path.GetDirectoryName(path);
            Folder? folder = Database.Connection
                .Table<Folder>()
                .FirstOrDefault(
                    folder => !folder.Archived && folder.Path == folderPath
                )
            ;

            if (folder == null) {
                throw new Exception(
                    "Attempting to create a database record for a file in an " +
                    $"untracked folder: {folderPath}"
                );
            }

            File? previousFile = Database.Connection
                .Table<File>()
                .Where(file => !file.Archived)
                .FirstOrDefault(file => file.Path == path)
            ;

            if (previousFile != null) {
                throw new Exception(
                    "Attempting to create a duplicate database record for a " +
                    $"tracked file: {path}"
                );
            }

            File file = new() {
                FolderId = folder.Id,
                Name = Path.GetFileName(path),
                Path = path,
            };

            Database.Connection.Insert(file);

            try {
                Diff.Init(file);
            } catch (Exception e) {
                Database.Connection.Delete(file, recursive: true);

                throw new Exception(
                    $"Something went wrong initializing a file.",
                    e
                );
            }
        }
    }

    public static void Update(File file) {
        lock (Database.WriteLock) {
            try {
                Diff.CreateDiff(file);
            } catch (Exception e) {
                throw new Exception("Failed to create diff.", e);
            }
        }
    }

    public static int Archive(Folder folder) {
        lock (Database.WriteLock) {
            int archivedFilesCount = -1;

            try {
                Database.Connection.RunInTransaction(() => {
                    int archivedFoldersCount = Database.Connection
                        .Execute(ARCHIVE_FOLDER_FOLDERS_SQL, folder.Id)
                    ;

                    if (archivedFoldersCount != 1) return;

                    folder.Archived = true;

                    archivedFilesCount = Database.Connection
                        .Execute(ARCHIVE_FOLDER_FILES_SQL, folder.Id)
                    ;
                });
            } catch (Exception e) {
                throw new Exception("There was an issue archiving a folder", e);
            }

            FolderWatcherService.StopWatching(folder);

            return archivedFilesCount;
        }
    }

    public static bool Archive(File file, bool restoreWithFolder = false) {
        lock (Database.WriteLock) {
            int archivedFilesCount = Database.Connection
                .Execute(ARCHIVE_FILE_SQL, restoreWithFolder, file.Id)
            ;

            bool archived = archivedFilesCount == 1;

            if (archived) {
                file.Archived = true;
                file.RestoreWithFolder = restoreWithFolder;
            }

            return archived;
        }
    }

    public static async Task Restore(Folder folder) {
        if (!folder.Archived) {
            throw new Exception(
                $"Attempting to restore an unarchived folder: {folder.Path}"
            );
        }

        if (Directory.Exists(folder.Path)) {
            throw new Exception(
                "Attempting to restore a folder at a path where a folder " +
                $"already exists: {folder.Path}"
            );
        }

        List<File> files;

        try {
            lock (Database.WriteLock) {
                folder.Archived = false;
                Database.Connection.Update(folder);

                Directory.CreateDirectory(folder.Path);

                files = Database.Connection
                    .Table<File>()
                    .Where(file =>
                        file.FolderId == folder.Id &&
                        file.Archived &&
                        file.RestoreWithFolder
                    )
                    .ToList()
                ;
            }

            foreach (File file in files) {
                try {
                    await Restore(file);
                } catch (Exception e) {
                    Console.WriteLine($"Could not restore {file.Path}: {e}");
                }
            }

            FolderWatcherService.Watch(folder);
        } catch (Exception e) {
            throw new Exception(
                "An error occurred while restoring a folder.",
                e
            );
        }
    }

    public static async Task Restore(File file) {
        try {
            await Diff.Restore(file);

            lock (Database.WriteLock) {
                file.Archived = false;
                file.RestoreWithFolder = false;
                Database.Connection.Update(file);
            }
        } catch (Exception e) {
            throw new Exception("An error occurred while restoring a file.", e);
        }
    }

    public static void Delete(Folder folder) {
        lock (Database.WriteLock) {
            if (!folder.Archived) {
                throw new Exception(
                    $"Attempting to delete an unarchived folder: {folder}"
                );
            }

            Database.Connection.GetChildren(folder, recursive: true);

            if (
                folder.Files == null ||
                folder.Files.Any(file => {
                    return file.Diffs == null ||
                        file.Branches == null ||
                        file.Diffs.Count() == 0 ||
                        file.Branches.Count() == 0
                    ;
                })
            ) {
                throw new Exception(
                    "Something went wrong retrieving a folder's files. " +
                    $"Folder Id: {folder.Id}"
                );
            }

            List<string> diffFiles = new();

            foreach (File file in folder.Files) {
                diffFiles.AddRange(Diff.GetDiffFiles(file));
            }


            Database.Connection.Delete(folder, recursive: true);

            foreach (string path in diffFiles) {
                try {
                    if (System.IO.File.Exists(path)) {
                        System.IO.File.Delete(path);
                    }
                } catch (Exception e) {
                    Console.WriteLine(
                        "An error occurred while deleting a diff file. \n" +
                        $"Error: {e}\n" +
                        $"File: {path}"
                    );
                }
            }
        }
    }

    public static void Delete(File file) {
        lock (Database.WriteLock) {
            if (!file.Archived) {
                throw new Exception(
                    $"Attempting to delete an unarchived file: {file}"
                );
            }

            Database.Connection.GetChildren(file, recursive: true);

            if (file.Diffs == null || file.Diffs.Count() == 0) {
                throw new Exception(
                    "Something went wrong retrieving a file's diffs. File " +
                    $"Id: {file.Id}"
                );
            }

            if (file.Branches == null || file.Branches.Count() == 0) {
                throw new Exception(
                    "Something went wrong retrieving a file's branches. File " +
                    $"Id: {file.Id}"
                );
            }

            List<string> diffFiles = Diff.GetDiffFiles(file);

            Database.Connection.Delete(file, recursive: true);

            foreach (string path in diffFiles) {
                try {
                    if (System.IO.File.Exists(path)) {
                        System.IO.File.Delete(path);
                    }
                } catch (Exception e) {
                    Console.WriteLine(
                        "An error occurred while deleting a diff file. \n" +
                        $"Error: {e}\n" +
                        $"File: {path}"
                    );
                }
            }
        }
    }

    public static void MarkRestoreWithFolder(
        Folder folder,
        IEnumerable<int> fileIds
    ) {
        lock (Database.WriteLock) {
            int[] ids = fileIds.ToArray();

            if (ids.Length == 0) return;

            Database.Connection.RunInTransaction(() => {
                foreach (int[] batch in ids.Chunk(MAX_FILE_IDS_PER_QUERY)) {
                    string batchedIds = string.Join(
                        ", ",
                        Enumerable.Repeat("?", batch.Length)
                    );
                    string sql = "" +
                        "UPDATE files SET restore_with_folder = 1 " +
                        $"WHERE id IN ({batchedIds}) " +
                        "AND folder_id = ? AND archived = 1"
                    ;
                    object[] args = batch
                        .Cast<object>()
                        .Append(folder.Id)
                        .ToArray()
                    ;

                    Database.Connection.Execute(sql, args);
                }
            });
        }
    }

    public static bool IsTrackable(string path) {
        try {
            FileAttributes attributes = System.IO.File.GetAttributes(path);
            return (attributes & EXCLUDED_ATTRIBUTES) == 0;
        } catch (FileNotFoundException) {
            return true; // File likely deleted. Let it through.
        } catch (DirectoryNotFoundException) {
            return true; // Parent folder likely deleted. Let it through.
        } catch (UnauthorizedAccessException) {
            return false; // Issues reading file. Skip it.
        } catch (IOException) {
            return false; // Other issues with the file. Skip it.
        }
    }
}
