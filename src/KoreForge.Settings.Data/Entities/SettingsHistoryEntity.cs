using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KoreForge.Settings.Data.Entities;

[Table("SettingsHistory", Schema = "dbo")]
public class SettingsHistoryEntity
{
    [Key]
    public long HistoryId { get; set; }
    public long? SettingId { get; set; }
    [MaxLength(200)] public string? ApplicationId { get; set; }
    [MaxLength(200)] public string? InstanceId { get; set; }
    [MaxLength(200)] public string? ClientAppVersion { get; set; }
    [Required, MaxLength(2048)] public string Key { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public byte[]? OldBinaryValue { get; set; }
    public string? NewValue { get; set; }
    public byte[]? NewBinaryValue { get; set; }
    public bool? OldIsSecret { get; set; }
    public bool? OldValueEncrypted { get; set; }
    public bool? NewIsSecret { get; set; }
    public bool? NewValueEncrypted { get; set; }
    public byte[]? RowVersionBefore { get; set; }
    public byte[]? RowVersionAfter { get; set; }
    [MaxLength(50)] public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedDate { get; set; }
    [MaxLength(20)] public string Operation { get; set; } = string.Empty; // Insert|Update|Delete|Rollback
}
