using System.ComponentModel.DataAnnotations;

namespace KF.Settings.Models;

public sealed record SettingUpsert
{
    public long? Id { get; init; }
    [Required]
    public string Key { get; init; } = string.Empty;
    public string? ApplicationId { get; init; }
    public string? InstanceId { get; init; }
    public string? ClientAppVersion { get; init; }
    public string? Value { get; init; }
    public byte[]? BinaryValue { get; init; }
    public bool IsSecret { get; init; }
    public bool EncryptValue { get; init; }
    public string ChangedBy { get; init; } = "unknown";
    public byte[]? ExpectedRowVersion { get; init; }
    public string? Comment { get; init; }
    public string? Notes { get; init; }
}
