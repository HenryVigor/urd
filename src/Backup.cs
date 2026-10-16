using SQLiteNetExtensions.Extensions;

public static class Backup {
    const FileAttributes EXCLUDED_ATTRIBUTES = FileAttributes.Directory |
        FileAttributes.Hidden |
        FileAttributes.System |
        FileAttributes.Device |
        FileAttributes.Offline |
        FileAttributes.ReparsePoint // TODO: Review exclusion of reparse points
    ;

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
                Name = path,
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
            }
            catch (Exception e) {
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
                .FirstOrDefault(folder => folder.Path == folderPath)
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
            }
            catch (Exception e) {
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
            }
            catch (Exception e) {
                throw new Exception("Failed to create diff.", e);
            }
        }
    }

    public static int Archive(Folder folder) {
        lock (Database.WriteLock) {
            int archivedFiles = -1;

            try {
                Database.Connection.RunInTransaction(() => {
                    string folderQuery = "UPDATE folders SET archived = 1 " +
                        "WHERE id = ? AND archived = 0"
                    ;
                    bool archived = Database.Connection.Execute(
                        folderQuery,
                        folder.Id
                    ) == 1;

                    if (!archived) return;

                    folder.Archived = true;

                    string filesQuery = "UPDATE files SET archived = 1 " +
                        "WHERE folder_id = ? AND archived = 0"
                    ;
                    archivedFiles = Database.Connection
                        .Execute(filesQuery, folder.Id)
                    ;
                });
            }
            catch (Exception e) {
                throw new Exception("There was an issue archiving a folder", e);
            }

            return archivedFiles;
        }
    }

    public static bool Archive(File file) {
        lock (Database.WriteLock) {
            string sql = "UPDATE files SET archived = 1 " +
                "WHERE id = ? AND archived = 0"
            ;
            bool archived = Database.Connection.Execute(sql, file.Id) == 1;

            if (archived) file.Archived = true;

            return archived;
        }
    }

    public static async Task Restore(File file) {
        try {
            await Diff.Restore(file);

            lock (Database.WriteLock) {
                file.Archived = false;
                Database.Connection.Update(file);
            }
        }
        catch (Exception e) {
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
                }
                catch (Exception e) {
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
                }
                catch (Exception e) {
                    Console.WriteLine(
                        "An error occurred while deleting a diff file. \n" +
                        $"Error: {e}\n" +
                        $"File: {path}"
                    );
                }
            }
        }
    }

    public static bool IsTrackable(string path) {
        try {
            FileAttributes attributes = System.IO.File.GetAttributes(path);
            return (attributes & EXCLUDED_ATTRIBUTES) == 0;
        }
        catch (FileNotFoundException) {
            return true; // File likely deleted. Let it through.
        }
        catch (DirectoryNotFoundException) {
            return true; // Parent folder likely deleted. Let it through.
        }
        catch (UnauthorizedAccessException) {
            return false; // Issues reading file. Skip it.
        }
        catch (IOException) {
            return false; // Other issues with the file. Skip it.
        }
    }
}
