using SQLite;
using SQLiteNetExtensions.Attributes;

// TODO: Consider option to mark folders as untracked. This might be
// counterintuitive, but it also may be useful for the user to be able to pause
// tracking in all of a folder's files.

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
}
