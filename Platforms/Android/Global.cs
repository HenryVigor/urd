using Android.Content;
using SQLite;

public static class Global {
    public static SQLiteConnection Database {
        get => DatabaseHandler.Instance.Database;
    }

    public static ComponentName? StartService(Type type, Context context) {
        var serviceIntent = new Intent(context, type);

        return OperatingSystem.IsAndroidVersionAtLeast(26)
            ? context.StartForegroundService(serviceIntent)
            : context.StartService(serviceIntent)
        ;
    }
}
