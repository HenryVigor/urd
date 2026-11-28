using System.Text.Json.Serialization;
using SQLite;
using SQLiteNetExtensions.Attributes;

[Table("file_diffs")]
public class FileDiff {
    [Column("id")]
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Column("file_id")]
    [ForeignKey(typeof(File))]
    public int FileId { get; set; }

    [ManyToOne]
    [JsonIgnore]
    public File File { get; set; }

    [Column("parent_id")]
    public int? ParentId { get; set; }

    [Column("is_snapshot")]
    public bool IsSnapshot { get; set; } = false;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}
