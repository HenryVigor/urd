using SQLite;
using SQLiteNetExtensions.Attributes;

[Table("folders")]
public class Folder {
    [Column("id")]
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("path")]
    public string Path { get; set; }

    [Column("archived")]
    public bool Archived { get; set; } = false;

    [OneToMany(CascadeOperations = CascadeOperation.CascadeDelete | CascadeOperation.CascadeRead)]
    public File[] Files { get; set; }

    [Ignore]
    public bool? IsRestorable { get; set; } = null;
}
