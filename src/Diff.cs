using System.IO.Compression;
using Octodiff.Core;
using Octodiff.Diagnostics;
using SQLiteNetExtensions.Extensions;

// TODO: Determine snapshot checkpoint method/frequency
public static class Diff {
    private static readonly HashSet<string> SkipCompressionExtensions = new(
        StringComparer.OrdinalIgnoreCase
    ) {
        // A list of common incompressable file types
        ".7z", ".aac", ".apk", ".avi", ".br", ".bz2", ".cab", ".cr3", ".dmg",
        ".dng", ".docx", ".epub", ".flac", ".gif", ".gz", ".heic", ".heif",
        ".iso", ".jar", ".jpeg", ".jpg", ".m4a", ".mkv", ".mov", ".mp3", ".mp4",
        ".msi", ".odp", ".ods", ".odt", ".ogg", ".opus", ".pak", ".pck", ".pdf",
        ".png", ".pptx", ".rar", ".tgz", ".vpk", ".war", ".webm",
        ".webp", ".wmv", ".xlsx", ".xz", ".zip",
    };

    static HashSet<int> ActiveRollbacks = new();

    public static void Init(File file) {
        if (!Directory.Exists(GetDirectoryPath())) {
            Directory.CreateDirectory(GetDirectoryPath());
        }

        FileDiff diff = new() {
            FileId = file.Id,
            ParentId = null,
            IsSnapshot = true,
            CreatedAt = DateTimeOffset.Now,
        };

        Database.Connection.Insert(diff);

        BuildSnapshot(diff);
        BuildSignature(diff);

        CreateBranch(diff, "main");

        SetHead(diff);
    }

    public static void CreateDiff(File file) {
        FileDiff? parent = Database.Connection.Find<FileDiff>(file.HeadDiffId);

        if (parent == null) {
            throw new Exception(
                "Something went wrong retrieving a file's head diff. File " +
                $"Id: {file.Id}"
            );
        }

        byte[] signatureBytes = BuildSignatureBytes(file.Path);
        byte[] parentSignature = System.IO.File.ReadAllBytes(
            GetSignaturePath(parent)
        );

        // Skip diff creation if there are no changes to the file
        if (signatureBytes.AsSpan().SequenceEqual(parentSignature)) return;

        // Create diff
        FileDiff diff = new() {
            FileId = file.Id,
            ParentId = parent.Id,
            CreatedAt = DateTimeOffset.Now
        };

        Database.Connection.Insert(diff);

        // Update branch
        Branch? branch = Database.Connection
            .Table<Branch>()
            .FirstOrDefault(branch => branch.TipDiffId == parent.Id)
        ;
        if (branch != null) {
            branch.TipDiffId = diff.Id;
            Database.Connection.Update(branch);
        } else CreateBranch(diff);

        BuildDelta(diff, parent);
        WriteSignature(diff, signatureBytes);

        SetHead(diff);
    }

    public static async Task RollBack(FileDiff diff) {
        EnsureDiffHasFile(diff);

        if (diff.File.Archived) {
            throw new Exception(
                $"Attempting a rollback on an archived file: {diff.File.Path}"
            );
        }

        await _Rollback(diff, overwrite: true);

        SetHead(diff);
    }

    public static async Task Restore(File file) {
        if (!file.Archived) {
            throw new Exception(
                $"Attempting to restore an unarchived file: {file.Path}"
            );
        }

        if (System.IO.File.Exists(file.Path)) {
            throw new Exception(
                "Attempting to restore a file at a path where a file already " +
                $"exists: {file.Path}"
            );
        }

        FileDiff? diff = Database.Connection.Find<FileDiff>(file.HeadDiffId);

        if (diff == null) {
            throw new Exception(
                "Something went wrong retrieving a file's head diff. File " +
                $"Id: {file.Id}"
            );
        }

        await _Rollback(diff, overwrite: false);
    }

    public static List<string> GetDiffFiles(File file) {
        if (file.Diffs == null) {
            Database.Connection.GetChild(file, f => f.Diffs);
        }

        if (file.Diffs == null) {
            throw new Exception(
                "Something went wrong retrieving a file's diffs. File Id: " +
                file.Id
            );
        }

        return file.Diffs
            .SelectMany(diff => new[] {
                diff.IsSnapshot ? GetSnapshotPath(diff) : GetDeltaPath(diff),
                GetSignaturePath(diff),
            })
            .ToList()
        ;
    }

    static void CreateBranch(FileDiff firstDiff, string? name = null) {
        Database.Connection.Insert(new Branch() {
            FileId = firstDiff.FileId,
            FirstDiffId = firstDiff.Id,
            TipDiffId = firstDiff.Id,
            Name = name ?? DateTimeOffset.Now.ToString(),
        });
    }

    static void BuildSnapshot(FileDiff diff) {
        EnsureDiffHasFile(diff);

        File file = diff.File;

        if (!System.IO.File.Exists(file.Path)) {
            throw new Exception(
                "Attempting to create a snapshot of a file that does not " +
                $"exist: {file.Path}"
            );
        }

        string path = GetSnapshotPath(diff);

        if (System.IO.File.Exists(path)) {
            throw new Exception(
                "Attempting to create a snapshot file where one already " +
                $"exists: {path}"
            );
        }

        if (!IsCompressible(file)) {
            // Skip compression and save a raw copy
            System.IO.File.Copy(file.Path, GetSnapshotPath(diff));
            return;
        }

        // Save a compressed copy
        CompressionLevel compressionLevel = GetCompressionLevel(file.Path);
        using FileStream originalStream = System.IO.File.OpenRead(file.Path);
        using FileStream snapshotStream = System.IO.File.Create(path);
        using BrotliStream brotliStream = new(snapshotStream, compressionLevel);
        originalStream.CopyTo(brotliStream);
    }

    static void BuildDelta(FileDiff diff, FileDiff? parentDiff = null) {
        EnsureDiffHasFile(diff);

        if (!System.IO.File.Exists(diff.File.Path)) {
            throw new Exception(
                "Attempting to create a delta using on a file that does not " +
                $"exist: {diff.File.Path}"
            );
        }

        parentDiff ??= Database.Connection.Find<FileDiff>(diff.ParentId);

        if (parentDiff == null) {
            throw new Exception(
                "Something went wrong retrieving a diff's parent. Diff Id: " +
                diff.Id
            );
        }

        string signaturePath = GetSignaturePath(parentDiff);

        if (!System.IO.File.Exists(signaturePath)) {
            throw new Exception(
                "Attempting to create a delta using a parent signature that " +
                $"does not exist: {signaturePath}"
            );
        }

        string deltaPath = GetDeltaPath(diff);

        if (System.IO.File.Exists(deltaPath)) {
            throw new Exception(
                "Attempting to create a delta where one already exists: " +
                deltaPath
            );
        }

        using FileStream fileStream = System.IO.File.OpenRead(diff.File.Path);
        using FileStream sigStream = System.IO.File.OpenRead(signaturePath);
        using FileStream deltaStream = System.IO.File.Create(deltaPath);

        NullProgressReporter reporter = new();
        SignatureReader signatureReader = new(sigStream, reporter);

        BinaryDeltaWriter writer = new(deltaStream);
        new DeltaBuilder().BuildDelta(fileStream, signatureReader, writer);
    }

    static void BuildSignature(FileDiff diff) {
        EnsureDiffHasFile(diff);

        if (!System.IO.File.Exists(diff.File.Path)) {
            throw new Exception(
                "Attempting to create a signature using on a file that does " +
                $"not exist: {diff.File.Path}"
            );
        }

        string path = GetSignaturePath(diff);

        if (System.IO.File.Exists(path)) {
            throw new Exception(
                "Attempting to create a signature where one already exists: " +
                path
            );
        }

        using FileStream fileStream = System.IO.File.OpenRead(diff.File.Path);
        using FileStream signatureStream = System.IO.File.Create(path);

        SignatureWriter writer = new(signatureStream);
        new SignatureBuilder().Build(fileStream, writer);
    }

    static byte[] BuildSignatureBytes(string path) {
        if (!System.IO.File.Exists(path)) {
            throw new Exception(
                "Attempting to create a signature using on a file that does " +
                $"not exist: {path}"
            );
        }

        using FileStream fileStream = System.IO.File.OpenRead(path);
        using MemoryStream signatureStream = new();

        SignatureWriter writer = new(signatureStream);
        new SignatureBuilder().Build(fileStream, writer);

        return signatureStream.ToArray();
    }

    static void WriteSignature(FileDiff diff, byte[] signatureBytes) {
        string path = GetSignaturePath(diff);

        if (System.IO.File.Exists(path)) {
            throw new Exception(
                "Attempting to create a signature where one already exists: " +
                path
            );
        }

        System.IO.File.WriteAllBytes(path, signatureBytes);
    }

    static string Reconstruct(FileDiff diff) {
        EnsureDiffHasFile(diff);

        // Get diff history from last snapshot to given diff
        List<FileDiff> pastDiffs;
        if (diff.IsSnapshot) pastDiffs = new();
        else {
            pastDiffs = Database.Connection
                .Table<FileDiff>()
                .Where(fileDiff => fileDiff.FileId == diff.FileId)
                .Where(fileDiff => fileDiff.CreatedAt < diff.CreatedAt)
                .OrderByDescending(fileDiff => fileDiff.CreatedAt)
                .ToList()
            ;
        }

        Stack<FileDiff> lineage = new();
        lineage.Push(diff);

        foreach (FileDiff pastDiff in pastDiffs) {
            FileDiff previous = lineage.Peek();

            if (previous.IsSnapshot) break;

            if (pastDiff.Id != previous.ParentId) continue;

            lineage.Push(pastDiff);
        }

        // Get temp file
        FileDiff snapshot = lineage.Pop();
        string snapshotPath = GetSnapshotPath(snapshot);
        string tempFile = Path.GetTempFileName();

        if (IsCompressible(diff.File)) {
            // Decompress snapshot into temp file
            CompressionMode compressionMode = CompressionMode.Decompress;
            using FileStream fileStream = System.IO.File.OpenRead(snapshotPath);
            using BrotliStream brotliStream = new(fileStream, compressionMode);
            using FileStream decompressStream = System.IO.File.Create(tempFile);
            brotliStream.CopyTo(decompressStream);
        } else {
            // Copy raw snapshot into temp file
            System.IO.File.Copy(snapshotPath, tempFile);
        }

        // Apply diffs to temp file
        while (lineage.Count > 0) {
            string deltaPath = GetDeltaPath(lineage.Pop());

            if (!System.IO.File.Exists(deltaPath)) {
                throw new Exception(
                    "Attempting a reconstruction using a delta that does not " +
                    $"exist: {deltaPath}"
                );
            }

            string tempNext = Path.GetTempFileName();

            using FileStream basisStream = System.IO.File.OpenRead(tempFile);
            using FileStream deltaStream = System.IO.File.OpenRead(deltaPath);
            using FileStream outputStream = System.IO.File.Create(tempNext);

            NullProgressReporter reporter = new();
            BinaryDeltaReader reader = new(deltaStream, reporter);
            new DeltaApplier().Apply(basisStream, reader, outputStream);

            System.IO.File.Delete(tempFile);
            tempFile = tempNext;
        }

        return tempFile;
    }

    static async Task _Rollback(FileDiff diff, bool overwrite) {
        EnsureDiffHasFile(diff);

        bool duplicateRollback = false;
        try {
            if (!ActiveRollbacks.Add(diff.File.Id)) {
                duplicateRollback = true;

                throw new Exception(
                    "Attempting a rollback for a file that already has a " +
                    $"rollback in progress: {diff.File.Path}"
                );
            }

            string tempFile = Reconstruct(diff);

            await FolderWatcherService.Lock(diff.File);

            try {
                System.IO.File.Move(
                    tempFile,
                    diff.File.Path,
                    overwrite: overwrite
                );
            }
            catch (IOException e) {
                throw new Exception(
                    "An I/O Exception occurred while attempting a rollback.",
                    e
                );
            }
        }
        catch (Exception e) {
            throw new Exception(
                "An error occurred while attempting a rollback.",
                e
            );
        }
        finally {
            if (!duplicateRollback) {
                ActiveRollbacks.Remove(diff.File.Id);
                FolderWatcherService.Unlock(diff.File);
            }
        }
    }

    static void SetHead(FileDiff diff) {
        EnsureDiffHasFile(diff);

        diff.File.HeadDiffId = diff.Id;
        Database.Connection.Update(diff.File);
    }

    static void EnsureDiffHasFile(FileDiff diff) {
        if (diff.File == null) Database.Connection.GetChild(diff, d => d.File);
        if (diff.File == null) {
            throw new Exception(
                "Something went wrong retrieving a diff's file. FileDiff Id: " +
                diff.Id
            );
        }
    }

    static string GetSnapshotPath(FileDiff diff) {
        EnsureDiffHasFile(diff);

        string extension = IsCompressible(diff.File)
            ? ".br"
            : Path.GetExtension(diff.File.Path)
        ;

        return Path.Combine(GetDirectoryPath(), $"{diff.Id}{extension}");
    }

    static string GetDeltaPath(FileDiff diff) {
        return Path.Combine(GetDirectoryPath(), $"{diff.Id}.octodelta");
    }

    static string GetSignaturePath(FileDiff diff) {
        return Path.Combine(GetDirectoryPath(), $"{diff.Id}.octosig");
    }

    static string GetDirectoryPath() {
        return Path.Combine(FileSystem.AppDataDirectory, "diffs");
    }

    static bool IsCompressible(File file) {
        string extension = Path.GetExtension(file.Path);
        return !SkipCompressionExtensions.Contains(extension);
    }

    static CompressionLevel GetCompressionLevel(string path) {
        long length = new FileInfo(path).Length;

        if (length < 2_000_000) return CompressionLevel.SmallestSize;

        if (length < 25_000_000) return CompressionLevel.Optimal;

        return CompressionLevel.Fastest;
    }
}
