namespace KF.Settings.Models;

public sealed record SettingRow(
    long Id,
    string? ApplicationId,
    string? InstanceId,
    string Key,
    string? Value,
    byte[]? BinaryValue,
    bool IsSecret,
    bool ValueEncrypted,
    string CreatedBy,
    DateTime CreatedDateUtc,
    string ModifiedBy,
    DateTime ModifiedDateUtc,
    string? Comment,
    string? Notes,
    byte[] RowVersion
);
