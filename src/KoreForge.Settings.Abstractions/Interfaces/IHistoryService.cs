using KoreForge.Settings.Models;

namespace KoreForge.Settings.Interfaces;

public interface IHistoryService
{
    Task<IReadOnlyList<SettingsHistoryRow>> GetHistoryAsync(long settingId, CancellationToken ct);
    Task RollbackAsync(string key, string? applicationId, string? instanceId, string? clientAppVersion, int versionIndex, string changedBy, CancellationToken ct);
}
