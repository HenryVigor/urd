using System.Text.Json.Serialization;
using SQLite;
using SQLiteNetExtensions.Attributes;

// TODO: Consider option to mark a file as untracked. This would stop backups of
// the file from being created. There would probably be an option to remove a
// file's backups when it is marked as untracked.

[Table("files")]
public class File {
    [Column("id")]
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Column("folder_id")]
    [ForeignKey(typeof(Folder))]
    public int FolderId { get; set; }

    [ManyToOne]
    [JsonIgnore]
    public Folder Folder { get; set; }

    [Column("head_diff_id")]
    public int? HeadDiffId { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("path")]
    public string Path { get; set; }

    [Column("archived")]
    public bool Archived { get; set; } = false;

    [OneToMany(CascadeOperations = CascadeOperation.CascadeDelete | CascadeOperation.CascadeRead)]
    public FileDiff[] Diffs { get; set; }

    [OneToMany(CascadeOperations = CascadeOperation.CascadeDelete | CascadeOperation.CascadeRead)]
    public Branch[] Branches { get; set; }
}
