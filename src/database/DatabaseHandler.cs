using SQLite;

public sealed class DatabaseHandler {
    public static DatabaseHandler Instance { get; private set; } = new();

    public readonly SQLiteConnection Database;

    public DatabaseHandler() {
        string databasePath = Path.Combine(
            FileSystem.AppDataDirectory,
            "urd.db3"
        );

        Database = new(databasePath);

        Database.CreateTable<Folder>();
        Database.CreateTable<File>();
    }
}
