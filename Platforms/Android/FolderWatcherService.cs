using System.Collections.Concurrent;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using SQLiteNetExtensions.Extensions;

// TODO: Review how this works if the root directory is set to be watched and
// whether that should be allowed. Ensure null Directory.GetParent() is handled
// properly.

// TODO: Split into multiple classes for clarity?

[Service(
    Exported = false,
    ForegroundServiceType = Android.Content.PM.ForegroundService.TypeSpecialUse
)]
[Property(
    "android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE",
    Value = "Monitors selected folders for file changes"
)]
public class FolderWatcherService : Service {
    const int UPDATE_DELAY = 300;
    const int NOTIFICATION_ID = 1001;
    const long RECENT_DELETE_WINDOW = 10_000;
    const string CHANNEL_ID = "folder_watcher_channel";
    const string BATTERY_PROMPTED_PREFERENCE = "battery_exemption_prompted";

    public static FolderWatcherService? Instance { get; private set; } = null;

    static bool Starting = false;

    NotificationManager? NotificationManager = null;
    readonly object NotificationLock = new();
    ConcurrentDictionary<string, FileSystemWatcher> Watchers = new();
    ConcurrentDictionary<string, FileSystemWatcher> ParentWatchers = new();
    readonly ConcurrentDictionary<string, byte> LockedFilePaths = new();
    readonly ConcurrentDictionary<string, byte> ScanningFolders = new();
    int StartId;
    int NotificationScheduled = 0;

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

    readonly Dictionary<string, RecentDeletes> RecentlyDeletedFiles = new();

    long LastRecentlyDeletedPrune = 0;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(
        Intent? intent,
        StartCommandFlags flags,
        int startId
    ) {
        StartId = startId;

        // Service breaks if foreground is not started.
        // Start here, then stop with service if no watchers initialized.
        StartForeground();

        if (Watchers.Count == 0) {
            StopService();
            return StartCommandResult.NotSticky;
        }

        return StartCommandResult.Sticky;
    }

    public override void OnCreate() {
        base.OnCreate();

        Instance = this;
        InitializeWatchers();
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

        foreach (CancellationTokenSource token in PendingChanges.Values) {
            token.Cancel();
        }
        PendingChanges.Clear();

        foreach (CancellationTokenSource token in PendingParentChanges.Values) {
            token.Cancel();
        }
        PendingParentChanges.Clear();

        base.OnDestroy();
    }

    /// <summary>Attempts to start the service</summary>
    /// <returns>
    ///     True if started.
    ///     False if already running or there no folders to watch.
    /// </returns>
    public static async Task<bool> TryStart() {
        if (Instance != null) return false;

        int unarchivedFoldersCount = Database.Connection
            .Table<Folder>()
            .Where(folder => !folder.Archived)
            .Count()
        ;

        if (unarchivedFoldersCount == 0) return false;

        return await Start();
    }

    public static async void Watch(Folder folder) {
        if (Instance == null) {
            await Start();
            return;
        }

        if (Instance.AddWatcher(folder)) Instance.UpdateNotification();
    }

    public static void StopWatching(Folder folder) {
        if (Instance == null) return;

        Instance.RemoveWatcher(folder.Path);
        Instance.UpdateNotification();
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

    static async Task<bool> Start() {
        if (Starting) return false;

        try {
            Starting = true;

            await EnsurePermissions();

            Context context = Android.App.Application.Context;
            Intent intent = new(context, typeof(FolderWatcherService));
            if (OperatingSystem.IsAndroidVersionAtLeast(26)) {
                context.StartForegroundService(intent);
            } else context.StartService(intent);
        } finally {
            Starting = false;
        }

        return true;
    }

    bool AddWatcher(Folder folder) => AddWatcher(folder.Path);

    bool AddWatcher(string path) {
        FileSystemWatcher? watcher = null;
        try {
            if (Watchers.ContainsKey(path) || !Directory.Exists(path)) {
                return false;
            }

            watcher = new(path);
            AttachFileEventHandlers(watcher, path);
            watcher.EnableRaisingEvents = true;

            if (!Watchers.TryAdd(path, watcher)) {
                watcher.Dispose();
                return false;
            }

            AddParentWatcher(path);
            return true;
        } catch (Exception e) {
            watcher?.Dispose();
            Android.Util.Log.Error(
                "FolderWatcher",
                $"Could not watch {path}: {e}"
            );

            return false;
        }
    }

    void AttachFileEventHandlers(FileSystemWatcher watcher, string path) {
        watcher.Deleted += (s, e) => QueueProcessFileUpdate(e.FullPath);
        watcher.Created += (s, e) => QueueProcessFileUpdate(e.FullPath);
        watcher.Changed += (s, e) => QueueProcessFileUpdate(e.FullPath);
        watcher.Renamed += (s, e) => {
            QueueProcessFileUpdate(e.OldFullPath);
            QueueProcessFileUpdate(e.FullPath);
        };
        watcher.Error += (s, e) => _FileError(path, watcher, e);
    }

    bool AddParentWatcher(string childPath) {
        string? path = Directory.GetParent(childPath)?.FullName;
        return path != null && AddParentWatcherAt(path);
    }

    bool AddParentWatcherAt(string path) {
        FileSystemWatcher? watcher = null;
        try {
            if (ParentWatchers.ContainsKey(path)) return false;

            watcher = new(path);
            AttachFolderEventHandlers(watcher, path);
            watcher.EnableRaisingEvents = true;

            if (!ParentWatchers.TryAdd(path, watcher)) {
                watcher.Dispose();
                return false;
            }

            return true;
        } catch (Exception e) {
            watcher?.Dispose();
            Android.Util.Log.Error(
                "FolderWatcher",
                $"Could not watch parent {path}: {e}")
            ;

            return false;
        }
    }

    void AttachFolderEventHandlers(FileSystemWatcher watcher, string path) {
        watcher.Deleted += (s, e) => QueueProcessFolderUpdate(e.FullPath);
        watcher.Created += (s, e) => QueueProcessFolderUpdate(e.FullPath);
        watcher.Changed += (s, e) => QueueProcessFolderUpdate(e.FullPath);
        watcher.Renamed += (s, e) => {
            QueueProcessFolderUpdate(e.OldFullPath);
            QueueProcessFolderUpdate(e.FullPath);
        };
        watcher.Error += (s, e) => _FolderError(path, watcher, e);
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
        } catch (System.OperationCanceledException) {
            // A new update event superseded this one
        } finally {
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
        } finally {
            updateLock.Release();
        }
    }

    void _FileDeleted(File file) {
        string? parentPath = Directory.GetParent(file.Path)?.FullName;

        if (parentPath == null) return;

        lock (Database.WriteLock) {
            bool folderDeleted = !Directory.Exists(parentPath);

            if (!Backup.Archive(file, restoreWithFolder: folderDeleted)) return;

            if (!folderDeleted) TrackRecentlyDeleted(parentPath, file.Id);
        }

        UpdateNotification();
    }

    void TrackRecentlyDeleted(string parentPath, int fileId) {
        long now = System.Environment.TickCount64;
        PruneRecentlyDeleted(now);

        if (
            !RecentlyDeletedFiles
                .TryGetValue(parentPath, out RecentDeletes? entry)
            ||
            now - entry.LastActivity > RECENT_DELETE_WINDOW
        ) {
            entry = new();
            RecentlyDeletedFiles[parentPath] = entry;
        }

        entry.Ids.Add(fileId);
        entry.LastActivity = now;
    }

    void PruneRecentlyDeleted(long now) {
        if (now - LastRecentlyDeletedPrune < RECENT_DELETE_WINDOW) return;

        LastRecentlyDeletedPrune = now;

        List<string> parentsToRemove = RecentlyDeletedFiles
            .Where(pair => now - pair.Value.LastActivity > RECENT_DELETE_WINDOW)
            .Select(pair => pair.Key)
            .ToList()
        ;
        foreach (string parent in parentsToRemove) {
            RecentlyDeletedFiles.Remove(parent);
        }
    }

    void _FileChanged(File file) {
        Backup.Update(file);
    }

    void _FileCreated(string path) {
        Backup.AddFile(path);

        UpdateNotification();
    }

    void _FileError(string path, FileSystemWatcher watcher, ErrorEventArgs e) {
        try {
            Exception exception = e.GetException();
            Android.Util.Log.Error(
                "FolderWatcher",
                $"Watcher error for {path}: {exception}"
            );

            if (
                !Watchers.TryGetValue(path, out FileSystemWatcher? current) ||
                current != watcher
            ) return;

            if (exception is InternalBufferOverflowException) {
                RescanFolder(path);
                return;
            }

            if (!Directory.Exists(path)) {
                QueueProcessFolderUpdate(path);
                return;
            }

            if (Watchers.TryRemove(path, out FileSystemWatcher? old)) {
                old.Dispose();
            }

            AddWatcher(path);
            RescanFolder(path);
        } catch (Exception ex) {
            watcher?.Dispose();
            Android.Util.Log.Error(
                "FolderWatcher",
                $"Could not resolve watcher error at {path}: {ex}"
            );
        }
    }

    /// <summary>
    ///     Waits until a folder's update events stop firing before determining
    ///     the final update type and processing.
    /// </summary>
    /// <param name="path">Path to the updated folder</param>
    void QueueProcessFolderUpdate(string path) {
        if (!Watchers.ContainsKey(path)) return;

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
        } catch (System.OperationCanceledException) {
            // A new update event superseded this one
        } finally {
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
        } finally {
            updateLock.Release();
        }
    }

    void _FolderDeleted(Folder folder) {
        long now = System.Environment.TickCount64;

        lock (Database.WriteLock) {
            // Always drop the entry, even if the archive below is a no-op
            RecentlyDeletedFiles.Remove(folder.Path, out RecentDeletes? recent);

            if (Backup.Archive(folder) == -1) return;

            if (
                recent != null &&
                now - recent.LastActivity <= RECENT_DELETE_WINDOW
            ) Backup.MarkRestoreWithFolder(folder, recent.Ids.ToList());
        }

        RemoveWatcher(folder.Path);
        UpdateNotification();
    }

    void _FolderError(
        string path,
        FileSystemWatcher watcher,
        ErrorEventArgs e
    ) {
        try {
            Exception exception = e.GetException();
            Android.Util.Log.Error(
                "FolderWatcher",
                $"Parent Watcher error for {path}: {exception}"
            );

            if (
                !ParentWatchers.TryGetValue(
                    path,
                    out FileSystemWatcher? current
                ) || current != watcher
            ) return;

            if (exception is InternalBufferOverflowException) {
                RescanParentFolder(path);
                return;
            }

            if (ParentWatchers.TryRemove(path, out FileSystemWatcher? old)) {
                old.Dispose();
            }

            if (Directory.Exists(path)) {
                AddParentWatcherAt(path);
                RescanParentFolder(path);
            }
        } catch (Exception ex) {
            watcher?.Dispose();
            Android.Util.Log.Error(
                "FolderWatcher",
                $"Could not resolve parent watcher error at {path}: {ex}"
            );
        }
    }

    void RescanParentFolder(string path) {
        IEnumerable<string> childPaths = Watchers.Keys
            .Where(childPath => {
                string? parent = Directory.GetParent(childPath)?.FullName;
                return parent != null && parent == path;
            })
        ;

        foreach (string childPath in childPaths) {
            QueueProcessFolderUpdate(childPath);
        }
    }

    void RemoveWatcher(string path) {
        if (!Watchers.TryRemove(path, out FileSystemWatcher? watcher)) return;

        watcher.Dispose();

        RemoveParentWatcher(path);

        if (Watchers.Count == 0) StopService();
    }

    void RemoveParentWatcher(string childPath) {
        string? path = Directory.GetParent(childPath)?.FullName;

        if (path == null) return;

        bool hasMoreChildren = Watchers.Any(watcher => {
            string? parentPath = Directory.GetParent(watcher.Key)?.FullName;

            return parentPath != null && parentPath == path;
        });

        if (hasMoreChildren) return;

        if (!ParentWatchers.TryRemove(path, out FileSystemWatcher? watcher)) {
            return;
        }

        watcher.Dispose();
    }

    void InitializeWatchers() {
        IEnumerable<Folder> folders = Database.Connection
            .Table<Folder>()
            .Where(folder => !folder.Archived)
        ;

        foreach (Folder folder in folders) {
            AddWatcher(folder);
            RescanFolder(folder.Path);
        }
    }

    void RescanFolder(string path) {
        if (!ScanningFolders.TryAdd(path, 0)) return;


        _ = Task.Run(() => {
            try {
                Folder? folder = Database.Connection
                    .GetAllWithChildren<Folder>(
                        folder => !folder.Archived && folder.Path == path
                    )
                    .FirstOrDefault()
                ;

                if (folder == null || !Directory.Exists(path)) return;

                IEnumerable<string> paths = folder.Files
                    .Select(file => file.Path)
                    .Concat(Directory.GetFiles(folder.Path))
                    .Distinct()
                ;
                foreach (string filePath in paths) {
                    QueueProcessFileUpdate(filePath);
                }
            } catch (Exception e) {
                Android.Util.Log.Error(
                    "FolderWatcher",
                    $"Folder rescan failed for {path}: {e}"
                );
            } finally {
                ScanningFolders.TryRemove(path, out _);
            }
        });
    }

    void UpdateNotification() {
        if (Interlocked.Exchange(ref NotificationScheduled, 1) == 1) return;

        _ = Task.Delay(500).ContinueWith(_ => {
            Interlocked.Exchange(ref NotificationScheduled, 0);

            try {
                lock (NotificationLock) {
                    NotificationManager?.Notify(
                        NOTIFICATION_ID,
                        BuildNotification()
                    );
                }
            } catch (Exception e) {
                Android.Util.Log.Error(
                    "FolderWatcher",
                    $"Could not update notification: {e}"
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
        int numWatchedFiles = Database.Connection
            .Table<File>()
            .Count(file => !file.Archived)
        ;

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

    public static async Task EnsurePermissions() {
        if (Platform.CurrentActivity == null) return;

        await MainThread.InvokeOnMainThreadAsync(async () => {
            await RequestNotificationPermission();

            RequestBatteryOptimizationExemption();
        });
    }

    static async Task RequestNotificationPermission() {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33)) return;

        await Permissions.RequestAsync<Permissions.PostNotifications>();
    }

    static void RequestBatteryOptimizationExemption() {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23)) return;

        Activity? activity = Platform.CurrentActivity;

        if (activity == null) return;

        PowerManager powerManager = (PowerManager)activity
            .GetSystemService(PowerService)!
        ;

        if (powerManager.IsIgnoringBatteryOptimizations(activity.PackageName)) {
            return;
        }

        if (Preferences.Get(BATTERY_PROMPTED_PREFERENCE, false)) return;

        Preferences.Set(BATTERY_PROMPTED_PREFERENCE, true);

        Intent intent = new(Settings.ActionRequestIgnoreBatteryOptimizations);
        intent.SetData(Android.Net.Uri.Parse(
            $"package:{activity.PackageName}"
        ));
        activity.StartActivity(intent);
    }

    sealed class RecentDeletes {
        public readonly HashSet<int> Ids = new();
        public long LastActivity;
    }
}
