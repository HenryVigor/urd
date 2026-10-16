using SQLite;

public sealed class Database {
    public static SQLiteConnection Connection { get => Instance.Db; }
    public static readonly object WriteLock = new();

    static Database Instance = new();

    readonly SQLiteConnection Db;

    public Database() {
        Db = new(Path.Combine(FileSystem.AppDataDirectory, "urd.db3"));

        Db.CreateTable<Folder>();
        Db.CreateTable<File>();
        Db.CreateTable<FileDiff>();
        Db.CreateTable<Branch>();
    }
}
