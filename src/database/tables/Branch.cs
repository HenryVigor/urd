using System.Text.Json.Serialization;
using SQLite;
using SQLiteNetExtensions.Attributes;

[Table("branches")]
public class Branch {
    [Column("id")]
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Column("file_id")]
    [ForeignKey(typeof(File))]
    public int FileId { get; set; }

    [Column("first_diff_id")]
    [Unique]
    public int FirstDiffId { get; set; }

    [Column("tip_diff_id")]
    [Unique]
    public int TipDiffId { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [ManyToOne]
    [JsonIgnore]
    public File File { get; set; }
}
