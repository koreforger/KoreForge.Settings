using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KF.Settings.Data.Entities;

[Table("Settings", Schema = "dbo")]
public class SettingEntity
{
    [Key]
    public long Id { get; set; }
    [MaxLength(200)] public string? ApplicationId { get; set; }
    [MaxLength(200)] public string? InstanceId { get; set; }
    [Required, MaxLength(2048)] public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public byte[]? BinaryValue { get; set; }
    public bool IsSecret { get; set; }
    public bool ValueEncrypted { get; set; }
    [MaxLength(50)] public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    [MaxLength(50)] public string ModifiedBy { get; set; } = string.Empty;
    public DateTime ModifiedDate { get; set; }
    [MaxLength(4000)] public string? Comment { get; set; }
    public string? Notes { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
