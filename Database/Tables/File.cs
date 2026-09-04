using SQLite;

[Table("files")]
public class File {
    [Column("id")]
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Column("folder_id")]
    public int FolderId { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("file_path")]
    [Unique]
    public string FilePath { get; set; }

    [Column("deleted")]
    public bool Deleted { get; set; } = false;
}
