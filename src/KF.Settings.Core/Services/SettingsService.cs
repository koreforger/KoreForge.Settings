using System.Data;
using System.Diagnostics.CodeAnalysis;
using KF.Settings.Data;
using KF.Settings.Data.Entities;
using KF.Settings.Errors;
using KF.Settings.Interfaces;
using KF.Settings.Metrics;
using KF.Settings.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KF.Settings.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly IDbContextFactory<KFSettingsDbContext> _factory;
    private readonly IMetricsRecorder _metrics;

    public SettingsService(IDbContextFactory<KFSettingsDbContext> factory, IMetricsRecorder metrics)
    { _factory = factory; _metrics = metrics; }

    public async Task<IReadOnlyList<SettingRow>> QueryAsync(SettingQuery filter, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var q = db.Settings.AsQueryable();
        if (!string.IsNullOrEmpty(filter.ApplicationId)) q = q.Where(s => s.ApplicationId == filter.ApplicationId);
        if (!string.IsNullOrEmpty(filter.InstanceId)) q = q.Where(s => s.InstanceId == filter.InstanceId);
        if (!string.IsNullOrEmpty(filter.KeyPrefix)) q = q.Where(s => s.Key.StartsWith(filter.KeyPrefix));
        if (filter.IsSecret.HasValue) q = q.Where(s => s.IsSecret == filter.IsSecret);
        if (filter.Skip.HasValue) q = q.Skip(filter.Skip.Value);
        if (filter.Take.HasValue) q = q.Take(filter.Take.Value);
        return (await q.AsNoTracking().ToListAsync(ct)).Select(Map).ToList();
    }

    public async Task<SettingRow?> GetAsync(long id, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return e is null ? null : Map(e);
    }

    public async Task<SettingRow> UpsertAsync(SettingUpsert request, CancellationToken ct)
    {
        if ((request.Value is null) == (request.BinaryValue is null))
            throw new ValidationFailureException("SettingUpsert", new[] { "Exactly one of Value or BinaryValue must be provided" });

        await using var db = await _factory.CreateDbContextAsync(ct);
        if (db.Database.IsSqlServer())
        {
            return await UpsertSqlServerAsync(db, request, ct);
        }

        // In-memory / non-SQL fallback
        var exists = await db.Settings.FirstOrDefaultAsync(s =>
            s.ApplicationId == request.ApplicationId &&
            s.InstanceId == request.InstanceId &&
            s.Key == request.Key, ct);

        if (exists != null)
        {
            if (request.ExpectedRowVersion is null) throw new MissingRowVersionException(exists.Key, exists.ApplicationId, exists.InstanceId);
            if (!exists.RowVersion.SequenceEqual(request.ExpectedRowVersion))
            {
                _metrics.Increment(MetricsNames.ConcurrencyConflict);
                throw new ConcurrencyConflictException(exists.Key, exists.ApplicationId, exists.InstanceId, request.ExpectedRowVersion, exists.RowVersion);
            }
            var before = exists.RowVersion.ToArray();
            var oldVal = exists.Value; var oldBin = exists.BinaryValue; var oldSec = exists.IsSecret; var oldEnc = exists.ValueEncrypted;
            exists.Value = request.Value; exists.BinaryValue = request.BinaryValue; exists.IsSecret = request.IsSecret;
            exists.ValueEncrypted = request.EncryptValue; exists.ModifiedBy = request.ChangedBy; exists.ModifiedDate = DateTime.UtcNow;
            exists.Comment = request.Comment; exists.Notes = request.Notes;
            await db.SaveChangesAsync(ct);
            db.SettingsHistory.Add(new SettingsHistoryEntity
            {
                SettingId = exists.Id, ApplicationId = exists.ApplicationId, InstanceId = exists.InstanceId, Key = exists.Key,
                OldValue = oldVal, OldBinaryValue = oldBin, OldIsSecret = oldSec, OldValueEncrypted = oldEnc,
                NewValue = exists.Value, NewBinaryValue = exists.BinaryValue, NewIsSecret = exists.IsSecret, NewValueEncrypted = exists.ValueEncrypted,
                RowVersionBefore = before, RowVersionAfter = exists.RowVersion, ChangedBy = request.ChangedBy, ChangedDate = DateTime.UtcNow,
                Operation = nameof(SettingOperation.Update)
            });
            await db.SaveChangesAsync(ct);
            return Map(exists);
        }

        if (request.ExpectedRowVersion != null) throw new MissingRowVersionException(request.Key, request.ApplicationId, request.InstanceId);
        var created = new SettingEntity
        {
            ApplicationId = request.ApplicationId, InstanceId = request.InstanceId, Key = request.Key,
            Value = request.Value, BinaryValue = request.BinaryValue, IsSecret = request.IsSecret, ValueEncrypted = request.EncryptValue,
            CreatedBy = request.ChangedBy, CreatedDate = DateTime.UtcNow, ModifiedBy = request.ChangedBy, ModifiedDate = DateTime.UtcNow,
            Comment = request.Comment, Notes = request.Notes
        };
        db.Settings.Add(created);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { throw new DuplicateKeyException(request.Key, request.ApplicationId, request.InstanceId); }
        db.SettingsHistory.Add(new SettingsHistoryEntity
        {
            SettingId = created.Id, ApplicationId = created.ApplicationId, InstanceId = created.InstanceId, Key = created.Key,
            NewValue = created.Value, NewBinaryValue = created.BinaryValue, NewIsSecret = created.IsSecret, NewValueEncrypted = created.ValueEncrypted,
            RowVersionAfter = created.RowVersion, ChangedBy = request.ChangedBy, ChangedDate = DateTime.UtcNow,
            Operation = nameof(SettingOperation.Insert)
        });
        await db.SaveChangesAsync(ct);
        return Map(created);
    }

    public async Task DeleteAsync(long id, string changedBy, byte[] expectedRowVersion, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entity = await db.Settings.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity == null) throw new NotFoundException(id.ToString(), null, null);
        if (!entity.RowVersion.SequenceEqual(expectedRowVersion))
        {
            _metrics.Increment(MetricsNames.ConcurrencyConflict);
            throw new ConcurrencyConflictException(entity.Key, entity.ApplicationId, entity.InstanceId, expectedRowVersion, entity.RowVersion);
        }
        var old = entity; var oldRv = old.RowVersion.ToArray();
        db.Settings.Remove(entity);
        await db.SaveChangesAsync(ct);
        db.SettingsHistory.Add(new SettingsHistoryEntity
        {
            SettingId = old.Id, ApplicationId = old.ApplicationId, InstanceId = old.InstanceId, Key = old.Key,
            OldValue = old.Value, OldBinaryValue = old.BinaryValue, OldIsSecret = old.IsSecret, OldValueEncrypted = old.ValueEncrypted,
            RowVersionBefore = oldRv, ChangedBy = changedBy, ChangedDate = DateTime.UtcNow,
            Operation = nameof(SettingOperation.Delete)
        });
        await db.SaveChangesAsync(ct);
    }

    [ExcludeFromCodeCoverage]
    private async Task<SettingRow> UpsertSqlServerAsync(KFSettingsDbContext db, SettingUpsert request, CancellationToken ct)
    {
        while (true)
        {
            IDbContextTransaction? tx = null;
            try
            {
                tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
                var existing = await db.Settings
                    .FromSqlRaw(@"SELECT TOP 1 * FROM dbo.Settings WITH (UPDLOCK, HOLDLOCK) WHERE ((ApplicationId IS NULL AND {0} IS NULL) OR ApplicationId = {0}) AND ((InstanceId IS NULL AND {1} IS NULL) OR InstanceId = {1}) AND [Key] = {2}",
                                 request.ApplicationId, request.InstanceId, request.Key)
                    .FirstOrDefaultAsync(ct);
                if (existing != null)
                {
                    if (request.ExpectedRowVersion is null) throw new MissingRowVersionException(existing.Key, existing.ApplicationId, existing.InstanceId);
                    if (!existing.RowVersion.SequenceEqual(request.ExpectedRowVersion))
                    {
                        _metrics.Increment(MetricsNames.ConcurrencyConflict);
                        throw new ConcurrencyConflictException(existing.Key, existing.ApplicationId, existing.InstanceId, request.ExpectedRowVersion, existing.RowVersion);
                    }
                    var oldValue = existing.Value; var oldBin = existing.BinaryValue; var oldSec = existing.IsSecret; var oldEnc = existing.ValueEncrypted; var beforeRv = existing.RowVersion.ToArray();
                    existing.Value = request.Value; existing.BinaryValue = request.BinaryValue; existing.IsSecret = request.IsSecret;
                    existing.ValueEncrypted = request.EncryptValue; existing.ModifiedBy = request.ChangedBy; existing.ModifiedDate = DateTime.UtcNow;
                    existing.Comment = request.Comment; existing.Notes = request.Notes;
                    try { await db.SaveChangesAsync(ct); }
                    catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { throw new DuplicateKeyException(existing.Key, existing.ApplicationId, existing.InstanceId); }
                    db.SettingsHistory.Add(new SettingsHistoryEntity
                    {
                        SettingId = existing.Id, ApplicationId = existing.ApplicationId, InstanceId = existing.InstanceId, Key = existing.Key,
                        OldValue = oldValue, OldBinaryValue = oldBin, OldIsSecret = oldSec, OldValueEncrypted = oldEnc,
                        NewValue = existing.Value, NewBinaryValue = existing.BinaryValue, NewIsSecret = existing.IsSecret, NewValueEncrypted = existing.ValueEncrypted,
                        RowVersionBefore = beforeRv, RowVersionAfter = existing.RowVersion, ChangedBy = request.ChangedBy, ChangedDate = DateTime.UtcNow,
                        Operation = nameof(SettingOperation.Update)
                    });
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return Map(existing);
                }
                else
                {
                    if (request.ExpectedRowVersion != null) throw new MissingRowVersionException(request.Key, request.ApplicationId, request.InstanceId);
                    var created = new SettingEntity
                    {
                        ApplicationId = request.ApplicationId, InstanceId = request.InstanceId, Key = request.Key,
                        Value = request.Value, BinaryValue = request.BinaryValue, IsSecret = request.IsSecret, ValueEncrypted = request.EncryptValue,
                        CreatedBy = request.ChangedBy, CreatedDate = DateTime.UtcNow, ModifiedBy = request.ChangedBy, ModifiedDate = DateTime.UtcNow,
                        Comment = request.Comment, Notes = request.Notes
                    };
                    db.Settings.Add(created);
                    try { await db.SaveChangesAsync(ct); }
                    catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                    {
                        await tx.RollbackAsync(ct);
                        throw new MissingRowVersionException(request.Key, request.ApplicationId, request.InstanceId);
                    }
                    db.SettingsHistory.Add(new SettingsHistoryEntity
                    {
                        SettingId = created.Id, ApplicationId = created.ApplicationId, InstanceId = created.InstanceId, Key = created.Key,
                        NewValue = created.Value, NewBinaryValue = created.BinaryValue, NewIsSecret = created.IsSecret, NewValueEncrypted = created.ValueEncrypted,
                        RowVersionAfter = created.RowVersion, ChangedBy = request.ChangedBy, ChangedDate = DateTime.UtcNow,
                        Operation = nameof(SettingOperation.Insert)
                    });
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return Map(created);
                }
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627);

    private static SettingRow Map(SettingEntity e) => new(
        e.Id, e.ApplicationId, e.InstanceId, e.Key, e.Value, e.BinaryValue,
        e.IsSecret, e.ValueEncrypted, e.CreatedBy, e.CreatedDate,
        e.ModifiedBy, e.ModifiedDate, e.Comment, e.Notes, e.RowVersion);
}
