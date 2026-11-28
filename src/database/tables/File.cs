using System.Text.Json.Serialization;
using SQLite;
using SQLiteNetExtensions.Attributes;

[Table("files")]
public class File {
    [Column("id")]
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Column("folder_id")]
    [ForeignKey(typeof(Folder))]
    [Indexed]
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

    [Column("restore_with_folder")]
    public bool RestoreWithFolder { get; set; } = false;

    [OneToMany(CascadeOperations = CascadeOperation.CascadeDelete | CascadeOperation.CascadeRead)]
    public FileDiff[] Diffs { get; set; }

    [OneToMany(CascadeOperations = CascadeOperation.CascadeDelete | CascadeOperation.CascadeRead)]
    public Branch[] Branches { get; set; }
}
