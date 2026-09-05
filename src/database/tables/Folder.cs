using SQLite;

[Table("folders")]
public class Folder {
    [Column("id")]
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("path")]
    [Unique]
    public string Path { get; set; }

    public static void Insert(Folder folder) {
        Global.Database.RunInTransaction(() => {
            // Create database record for folder
            Global.Database.Insert(folder);

            // Create database records for folder's current files
            IEnumerable<File> files = Directory
                .GetFiles(folder.Path)
                .Select(path => new File {
                    FolderId = folder.Id,
                    Name = System.IO.Path.GetFileName(path),
                    FilePath = path
                })
            ;
            Global.Database.InsertAll(files);
        });
    }
}
