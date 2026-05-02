using KoreForge.Settings.Data;
using KoreForge.Settings.Data.Entities;
using KoreForge.Settings.Errors;
using KoreForge.Settings.Interfaces;
using KoreForge.Settings.Models;
using Microsoft.EntityFrameworkCore;

namespace KoreForge.Settings.Core.Services;

public sealed class HistoryService : IHistoryService
{
    private readonly IDbContextFactory<KoreForgeSettingsDbContext> _factory;

    public HistoryService(IDbContextFactory<KoreForgeSettingsDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<SettingsHistoryRow>> GetHistoryAsync(long settingId, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var hist = await db.SettingsHistory
            .Where(h => h.SettingId == settingId)
            .OrderByDescending(h => h.HistoryId)
            .AsNoTracking()
            .ToListAsync(ct);
        return hist.Select(Map).ToList();
    }

    public async Task RollbackAsync(string key, string? applicationId, string? instanceId, string? clientAppVersion, int versionIndex, string changedBy, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var histOrdered = await db.SettingsHistory
            .Where(h => h.Key == key && h.ApplicationId == applicationId && h.InstanceId == instanceId && h.ClientAppVersion == clientAppVersion)
            .OrderByDescending(h => h.HistoryId).ToListAsync(ct);
        if (versionIndex < 0 || versionIndex >= histOrdered.Count) throw new ArgumentOutOfRangeException(nameof(versionIndex));
        var target = histOrdered[versionIndex];

        var current = await db.Settings.FirstOrDefaultAsync(s => s.Key == key && s.ApplicationId == applicationId && s.InstanceId == instanceId && s.ClientAppVersion == clientAppVersion, ct);
        if (target.Operation is "Update" or "Rollback" or "Insert")
        {
            if (current == null) throw new RollbackConflictException(key, target.ApplicationId, target.InstanceId, target.RowVersionAfter, null);
            if (target.RowVersionAfter != null && !current.RowVersion.SequenceEqual(target.RowVersionAfter))
                throw new RollbackConflictException(key, target.ApplicationId, target.InstanceId, target.RowVersionAfter, current.RowVersion);
            var beforeRv = current.RowVersion.ToArray();
            if ((target.OldValue is null) == (target.OldBinaryValue is null))
                throw new ValidationFailureException("Rollback", new[] { "History entry invalid: OldValue XOR OldBinaryValue expectation violated" });
            current.Value = target.OldValue;
            current.BinaryValue = target.OldBinaryValue;
            current.IsSecret = target.OldIsSecret ?? current.IsSecret;
            current.ValueEncrypted = target.OldValueEncrypted ?? current.ValueEncrypted;
            current.ModifiedBy = changedBy;
            current.ModifiedDate = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            db.SettingsHistory.Add(new SettingsHistoryEntity
            {
                SettingId = current.Id,
                ApplicationId = current.ApplicationId,
                InstanceId = current.InstanceId,
                ClientAppVersion = current.ClientAppVersion,
                Key = current.Key,
                OldValue = target.NewValue,
                OldBinaryValue = target.NewBinaryValue,
                OldIsSecret = target.NewIsSecret,
                OldValueEncrypted = target.NewValueEncrypted,
                NewValue = current.Value,
                NewBinaryValue = current.BinaryValue,
                NewIsSecret = current.IsSecret,
                NewValueEncrypted = current.ValueEncrypted,
                RowVersionBefore = beforeRv,
                RowVersionAfter = current.RowVersion,
                ChangedBy = changedBy,
                ChangedDate = DateTime.UtcNow,
                Operation = nameof(SettingOperation.Rollback)
            });
        }
        else if (target.Operation == "Delete")
        {
            if (current != null) throw new RollbackConflictException(key, target.ApplicationId, target.InstanceId, null, current.RowVersion);
            if ((target.OldValue is null) == (target.OldBinaryValue is null))
                throw new ValidationFailureException("Rollback", new[] { "History entry invalid: OldValue XOR OldBinaryValue expectation violated" });
            var restored = new SettingEntity
            {
                ApplicationId = target.ApplicationId,
                InstanceId = target.InstanceId,
                ClientAppVersion = target.ClientAppVersion,
                Key = target.Key,
                Value = target.OldValue,
                BinaryValue = target.OldBinaryValue,
                IsSecret = target.OldIsSecret ?? false,
                ValueEncrypted = target.OldValueEncrypted ?? false,
                CreatedBy = changedBy,
                CreatedDate = DateTime.UtcNow,
                ModifiedBy = changedBy,
                ModifiedDate = DateTime.UtcNow
            };
            db.Settings.Add(restored);
            await db.SaveChangesAsync(ct);
            db.SettingsHistory.Add(new SettingsHistoryEntity
            {
                SettingId = restored.Id,
                ApplicationId = restored.ApplicationId,
                InstanceId = restored.InstanceId,
                ClientAppVersion = restored.ClientAppVersion,
                Key = restored.Key,
                NewValue = restored.Value,
                NewBinaryValue = restored.BinaryValue,
                NewIsSecret = restored.IsSecret,
                NewValueEncrypted = restored.ValueEncrypted,
                RowVersionAfter = restored.RowVersion,
                ChangedBy = changedBy,
                ChangedDate = DateTime.UtcNow,
                Operation = nameof(SettingOperation.Rollback)
            });
        }
        else
        {
            throw new InvalidOperationException("Unknown history operation");
        }
        await db.SaveChangesAsync(ct);
    }

    private static SettingsHistoryRow Map(SettingsHistoryEntity h) => new(
        h.HistoryId,
        h.SettingId,
        h.ApplicationId,
        h.InstanceId,
        h.ClientAppVersion,
        h.Key,
        h.OldValue,
        h.OldBinaryValue,
        h.NewValue,
        h.NewBinaryValue,
        h.OldIsSecret,
        h.OldValueEncrypted,
        h.NewIsSecret,
        h.NewValueEncrypted,
        h.RowVersionBefore,
        h.RowVersionAfter,
        h.ChangedBy,
        h.ChangedDate,
        Enum.TryParse<SettingOperation>(h.Operation, out var op) ? op : SettingOperation.Update
    );
}
