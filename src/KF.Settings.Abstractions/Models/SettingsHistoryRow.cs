namespace KF.Settings.Models;

public sealed record SettingsHistoryRow(
    long HistoryId,
    long? SettingId,
    string? ApplicationId,
    string? InstanceId,
    string Key,
    string? OldValue,
    byte[]? OldBinaryValue,
    string? NewValue,
    byte[]? NewBinaryValue,
    bool? OldIsSecret,
    bool? OldValueEncrypted,
    bool? NewIsSecret,
    bool? NewValueEncrypted,
    byte[]? RowVersionBefore,
    byte[]? RowVersionAfter,
    string ChangedBy,
    DateTime ChangedDateUtc,
    SettingOperation Operation
);
