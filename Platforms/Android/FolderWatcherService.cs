using Android.App;
using Android.Content;
using Android.OS;

[Service(
    Exported = false,
    ForegroundServiceType = Android.Content.PM.ForegroundService.TypeSpecialUse
)]
public class FolderWatcherService : Service {
    const int NOTIFICATION_ID = 1001;
    const string CHANNEL_ID = "folder_watcher_channel";

    public static FolderWatcherService? Instance { get; private set; } = null;

    int NumWatchedFiles = 0;
    NotificationManager? NotificationManager = null;
    Dictionary<string, FileSystemWatcher> Watchers = new();

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(
        Intent? intent,
        StartCommandFlags flags,
        int startId
    ) {
        Instance = this;

        List<string> paths = Global
            .Database
            .Table<Folder>()
            .Select(folder => folder.Path)
            .ToList()
        ;
        foreach (string path in paths) AddWatcher(path);

        Initialize();

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy() {
        if (Instance == this) Instance = null;

        foreach (FileSystemWatcher watcher in Watchers.Values) {
            watcher.Dispose();
        }
        base.OnDestroy();
    }

    public static void StartWatching(string path) {
        if (Instance == null) return;

        if (Instance.AddWatcher(path)) Instance.UpdateNotification();
    }

    bool AddWatcher(string path) {
        if (Watchers.ContainsKey(path)) return false;

        NumWatchedFiles += Directory.EnumerateFiles(path).Count();

        FileSystemWatcher watcher = new(path) { EnableRaisingEvents = true };

        watcher.Deleted += (s, e) => {
            File? file = Global
                .Database
                .Table<File>()
                .FirstOrDefault(file => file.FilePath == e.FullPath)
            ;

            if (file == null) return;

            file.Deleted = true;

            Global.Database.Update(file);

            NumWatchedFiles--;
            UpdateNotification();
        };

        watcher.Created += (s, e) => {
            if (Directory.Exists(e.FullPath)) return;

            Global.Database.Insert(new File {
                FolderId = Global
                    .Database
                    .Table<Folder>()
                    .First(folder => folder.Path == path)
                    .Id
                ,
                Name = Path.GetFileName(e.FullPath),
                FilePath = e.FullPath,
            });

            NumWatchedFiles++;
            UpdateNotification();
        };

        watcher.Changed += (s, e) => {
            File? file = Global
                .Database
                .Table<File>()
                .FirstOrDefault(file => file.FilePath == e.FullPath)
            ;

            if (file == null) return;

            // TODO: Implement file change behavior
        };

        watcher.Renamed += (s, e) => {
            File? file = Global
                .Database
                .Table<File>()
                .FirstOrDefault(file => file.FilePath == e.OldFullPath)
            ;

            if (file == null) return;

            file.FilePath = e.FullPath;

            Global.Database.Update(file);
        };

        watcher.Error += (s, e) => {
            Exception error = e.GetException();

            Android.Util.Log.Error(
                "FolderWatcher",
                $"Watcher failed for {path}: {error}"
            );

            try {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
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
        };

        Watchers.Add(path, watcher);

        return true;
    }

    void RemoveWatcher(string path) {
        NumWatchedFiles -= Directory.EnumerateFiles(path).Count();
        Watchers.Remove(path);
    }

    void Initialize() {
        InitializeNotification();
        StartService();
    }

    void InitializeNotification() {
        NotificationManager = (NotificationManager)GetSystemService(
            NotificationService
        )!;

        if (OperatingSystem.IsAndroidVersionAtLeast(26)) {
            NotificationManager.CreateNotificationChannel(
                new(CHANNEL_ID, "Folder watcher", NotificationImportance.Low)
            );
        }
    }

    void UpdateNotification() {
        NotificationManager!.Notify(NOTIFICATION_ID, BuildNotification());
    }

    void StartService() {
        if (OperatingSystem.IsAndroidVersionAtLeast(34)) {
            StartForeground(
                NOTIFICATION_ID,
                BuildNotification(),
                Android.Content.PM.ForegroundService.TypeSpecialUse
            );
        } else StartForeground(NOTIFICATION_ID, BuildNotification());
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
        string fileInflected = NumWatchedFiles == 1 ? "file" : "files";

        return $"Watching {NumWatchedFiles} {fileInflected} for changes";
    }
}
