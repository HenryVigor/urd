using System.Collections.Concurrent;
using System.Text.Json;
using Android.App;
using Android.Content;
using Android.OS;

// TODO: Handle filesystem changes that occur while tracking is disabled / the service isn't running
[Service(
    Exported = false,
    ForegroundServiceType = Android.Content.PM.ForegroundService.TypeSpecialUse
)]
[Property(
    "android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE",
    Value = "Monitors configured folders for file changes"
)]
public class FolderWatcherService : Service {
    const int UPDATE_DELAY = 300;
    const int NOTIFICATION_ID = 1001;
    const string CHANNEL_ID = "folder_watcher_channel";
    const string STOP_SERVICE_ACTION = "stop_service";

    public static FolderWatcherService? Instance { get; private set; } = null;

    int NumWatchedFiles = 0;
    NotificationManager? NotificationManager = null;
    readonly object NotificationLock = new();
    CancellationTokenSource? NotificationCancellation = null;
    ConcurrentDictionary<string, FileSystemWatcher> Watchers = new();
    ConcurrentDictionary<string, FileSystemWatcher> ParentWatchers = new();
    readonly ConcurrentDictionary<string, byte> LockedFilePaths = new();
    int StartId;

    readonly ConcurrentDictionary<
        string,
        CancellationTokenSource
    > PendingChanges = new();

    readonly ConcurrentDictionary<
        string,
        CancellationTokenSource
    > PendingParentChanges = new();

    readonly ConcurrentDictionary<
        string,
        SemaphoreSlim
    > FileUpdateLocks = new();

    readonly ConcurrentDictionary<
        string,
        SemaphoreSlim
    > FolderUpdateLocks = new();

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(
        Intent? intent,
        StartCommandFlags flags,
        int startId
    ) {
        Instance = this;
        StartId = startId;

        InitializeWatchers();

        // Service breaks if foreground is not started.
        // Start here, then stop with service if no watchers initialized.
        StartForeground();

        if (Watchers.Count == 0) {
            StopService();
            return StartCommandResult.NotSticky;
        }

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy() {
        if (Instance == this) Instance = null;

        foreach (FileSystemWatcher watcher in Watchers.Values) {
            watcher.Dispose();
        }
        Watchers.Clear();

        foreach (FileSystemWatcher watcher in ParentWatchers.Values) {
            watcher.Dispose();
        }
        ParentWatchers.Clear();

        base.OnDestroy();
    }

    public static void Start() {
        Context context = Android.App.Application.Context;
        Intent intent = new(context, typeof(FolderWatcherService));

        if (OperatingSystem.IsAndroidVersionAtLeast(26)) {
            context.StartForegroundService(intent);
        } else context.StartService(intent);
    }

    public static void Watch(Folder folder) {
        if (Instance == null) {
            Start();
            return;
        }

        if (Instance.AddWatcher(folder)) Instance.UpdateNotification();
    }

    public static async Task<bool> Lock(File file) {
        if (Instance == null) return false;

        string path = file.Path;

        if (Instance.LockedFilePaths.ContainsKey(path)) return false;

        while (
            Instance != null &&
            Instance.PendingChanges.Keys.Contains(path)
        ) await Task.Delay(UPDATE_DELAY);

        return Instance != null && Instance.LockedFilePaths.TryAdd(path, 0);
    }

    public static bool Unlock(File file) {
        if (Instance == null) return false;

        return Instance.LockedFilePaths.TryRemove(file.Path, out _);
    }

    bool AddWatcher(Folder folder) => AddWatcher(folder.Path);

    bool AddWatcher(string path) {
        if (Watchers.ContainsKey(path) || !Directory.Exists(path)) return false;

        Interlocked.Add(ref NumWatchedFiles, GetFileCount(path));

        FileSystemWatcher watcher = new(path) { EnableRaisingEvents = true };

        watcher.Deleted += (s, e) => QueueProcessFileUpdate(e.FullPath);
        watcher.Created += (s, e) => QueueProcessFileUpdate(e.FullPath);
        watcher.Changed += (s, e) => QueueProcessFileUpdate(e.FullPath);
        watcher.Renamed += (s, e) => {
            QueueProcessFileUpdate(e.OldFullPath);
            QueueProcessFileUpdate(e.FullPath);
        };
        // watcher.Error += (s, e) => _FileError(e, path, watcher);

        Watchers.TryAdd(path, watcher);

        AddParentWatcher(path);

        return true;
    }

    bool AddParentWatcher(string childPath) {
        string? path = Directory.GetParent(childPath)?.FullName;

        if (path == null || ParentWatchers.ContainsKey(path)) return false;

        FileSystemWatcher watcher = new(path) { EnableRaisingEvents = true };

        watcher.Deleted += (s, e) => QueueProcessFolderUpdate(e.FullPath);
        watcher.Created += (s, e) => QueueProcessFolderUpdate(e.FullPath);
        watcher.Changed += (s, e) => QueueProcessFolderUpdate(e.FullPath);
        watcher.Renamed += (s, e) => {
            QueueProcessFolderUpdate(e.OldFullPath);
            QueueProcessFolderUpdate(e.FullPath);
        };
        // watcher.Error += (s, e) => _FolderError(e, path, watcher);

        ParentWatchers.TryAdd(path, watcher);

        return true;
    }

    /// <summary>
    ///     Waits until a file's update events stop firing before determining
    ///     the final update type and processing.
    /// </summary>
    /// <param name="path">Path to the updated file</param>
    void QueueProcessFileUpdate(string path) {
        if (LockedFilePaths.ContainsKey(path)) return;

        if (!Backup.IsTrackable(path)) return;

        bool resetTimer = PendingChanges
            .TryRemove(path, out CancellationTokenSource? previous)
        ;
        if (resetTimer) previous?.Cancel();

        CancellationTokenSource cancellation = new();
        PendingChanges[path] = cancellation;

        _ = DebounceFileUpdate(path, cancellation);
    }

    async Task DebounceFileUpdate(
        string path,
        CancellationTokenSource cancellation
    ) {
        try {
            await Task.Delay(UPDATE_DELAY, cancellation.Token);

            await ProcessFileUpdate(path);
        }
        catch (System.OperationCanceledException) {
            // A new update event superseded this one
        }
        finally {
            PendingChanges.TryRemove(new(path, cancellation));
            cancellation.Dispose();
        }
    }

    async Task ProcessFileUpdate(string path) {
        SemaphoreSlim updateLock = FileUpdateLocks.GetOrAdd(
            path,
            _ => new SemaphoreSlim(1, 1)
        );

        await updateLock.WaitAsync();
        try {
            File? file = Database.Connection
                .Table<File>()
                .Where(file => !file.Archived)
                .FirstOrDefault(file => file.Path == path)
            ;

            bool fileOnDisk = System.IO.File.Exists(path);

            if (file != null) {
                if (fileOnDisk) _FileChanged(file);
                else _FileDeleted(file);
            } else if (fileOnDisk) _FileCreated(path);
        }
        finally {
            updateLock.Release();
        }
    }

    void _FileDeleted(File file) {
        if (!Backup.Archive(file)) return;

        Interlocked.Decrement(ref NumWatchedFiles);
        UpdateNotification();
    }

    void _FileChanged(File file) {
        Backup.Update(file);
    }

    void _FileCreated(string path) {
        Backup.AddFile(path);

        Interlocked.Increment(ref NumWatchedFiles);
        UpdateNotification();
    }

    void _FileError(ErrorEventArgs e, string path, FileSystemWatcher watcher) {
        Console.WriteLine("_FileError: " + JsonSerializer.Serialize(e));


        // TODO: Review this

        Exception error = e.GetException();

        Android.Util.Log.Error(
            "FolderWatcher",
            $"Watcher failed for {path}: {error}"
        );

        try {
            watcher.EnableRaisingEvents = false;
            RemoveWatcher(path);
            AddWatcher(path);
            UpdateNotification();
        }
        catch (Exception restartError) {
            Android.Util.Log.Error(
                "FolderWatcher",
                $"Could not restart watcher for {path}: {restartError}"
            );
        }
    }

    /// <summary>
    ///     Waits until a folder's update events stop firing before determining
    ///     the final update type and processing.
    /// </summary>
    /// <param name="path">Path to the updated folder</param>
    void QueueProcessFolderUpdate(string path) {
        bool resetTimer = PendingParentChanges
            .TryRemove(path, out CancellationTokenSource? previous)
        ;
        if (resetTimer) previous?.Cancel();

        CancellationTokenSource cancellation = new();
        PendingParentChanges[path] = cancellation;

        _ = DebounceFolderUpdate(path, cancellation);
    }

    async Task DebounceFolderUpdate(
        string path,
        CancellationTokenSource cancellation
    ) {
        try {
            await Task.Delay(UPDATE_DELAY, cancellation.Token);

            await ProcessFolderUpdate(path);
        }
        catch (System.OperationCanceledException) {
            // A new update event superseded this one
        }
        finally {
            PendingParentChanges.TryRemove(new(path, cancellation));
            cancellation.Dispose();
        }
    }

    async Task ProcessFolderUpdate(string path) {
        SemaphoreSlim updateLock = FolderUpdateLocks.GetOrAdd(
            path,
            _ => new SemaphoreSlim(1, 1)
        );

        await updateLock.WaitAsync();
        try {
            Folder? folder = Database.Connection
                .Table<Folder>()
                .Where(folder => !folder.Archived)
                .FirstOrDefault(file => file.Path == path)
            ;

            if (folder != null && !Directory.Exists(path)) {
                _FolderDeleted(folder);
            }
        }
        finally {
            updateLock.Release();
        }
    }

    void _FolderDeleted(Folder folder) {
        int archived = Backup.Archive(folder);

        if (archived == -1) return;

        RemoveWatcher(folder.Path, archived);
        UpdateNotification();
    }

    void RemoveWatcher(string path, int fileCount = -1) {
        fileCount = fileCount >= 0 ? fileCount : GetFileCount(path);
        Interlocked.Add(ref NumWatchedFiles, -fileCount);

        Watchers[path].Dispose();
        if (!Watchers.TryRemove(path, out FileSystemWatcher? watcher)) return;

        watcher.Dispose();

        RemoveParentWatcher(path);

        if (Watchers.Count == 0) StopService();
    }

    void RemoveParentWatcher(string childPath) {
        string? path = Directory.GetParent(childPath)?.FullName;

        if (path == null) return;

        if (!ParentWatchers.TryRemove(path, out FileSystemWatcher? watcher)) {
            return;
        }

        watcher.Dispose();
    }

    void InitializeWatchers() {
        List<Folder> folders = Database.Connection
            .Table<Folder>()
            .Where(folder => !folder.Archived)
            .ToList()
        ;
        foreach (Folder folder in folders) AddWatcher(folder);
    }

    void UpdateNotification() {
        CancellationTokenSource cts = new();
        Interlocked.Exchange(ref NotificationCancellation, cts)?.Cancel();

        _ = Task.Delay(100, cts.Token).ContinueWith(t => {
            if (t.IsCanceled) return;
            lock (NotificationLock) {
                NotificationManager?.Notify(
                    NOTIFICATION_ID,
                    BuildNotification()
                );
            }
        });
    }

    void StartForeground() {
        // Initialize notification
        NotificationManager = (NotificationManager)GetSystemService(
            NotificationService
        )!;

        if (OperatingSystem.IsAndroidVersionAtLeast(26)) {
            NotificationManager.CreateNotificationChannel(
                new(CHANNEL_ID, "Folder watcher", NotificationImportance.Low)
            );
        }

        // Start foreground
        if (OperatingSystem.IsAndroidVersionAtLeast(34)) {
            StartForeground(
                NOTIFICATION_ID,
                BuildNotification(),
                Android.Content.PM.ForegroundService.TypeSpecialUse
            );
        } else StartForeground(NOTIFICATION_ID, BuildNotification());
    }

    void StopService() {
        if (OperatingSystem.IsAndroidVersionAtLeast(24)) {
            StopForeground(StopForegroundFlags.Remove);
        } else StopForeground(removeNotification: true);

        NotificationManager = null;

        StopSelf(StartId);
    }

    Notification BuildNotification() {
        return GetNotificationBuilder()
            .SetContentTitle("Backup service active")
            .SetContentText(GetNotificationText())
            .SetSmallIcon(Android.Resource.Drawable.IcMenuInfoDetails)
            .SetContentIntent(GetNotificationPendingIntent())
            .SetAutoCancel(true)
            .SetOngoing(true)
            .Build()
        ;
    }

    Notification.Builder GetNotificationBuilder() {
        bool useChannel = OperatingSystem.IsAndroidVersionAtLeast(26);
        return useChannel ? new(this, CHANNEL_ID) : new(this);
    }

    PendingIntent GetNotificationPendingIntent() {
        Intent intent = new(this, typeof(Urd.MainActivity));

        PendingIntentFlags flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) {
            flags |= PendingIntentFlags.Immutable;
        }

        return PendingIntent.GetActivity(this, 0, intent, flags)!;
    }

    string GetNotificationText() {
        int numWatchedFiles = Volatile.Read(ref NumWatchedFiles);

        string fileInflected = numWatchedFiles == 1 ? "file" : "files";
        string folderInflected = Watchers.Count == 1 ? "folder" : "folders";

        if (numWatchedFiles == 0) {
            // "Watching X folders"
            return $"Watching {Watchers.Count} {folderInflected}";
        }

        // "Watching X files in Y folders"
        return $"Watching {numWatchedFiles} {fileInflected} in " +
            $"{Watchers.Count} {folderInflected}"
        ;
    }

    int GetFileCount(string path) {
        return Directory.EnumerateFiles(path).Count(Backup.IsTrackable);
    }
}
