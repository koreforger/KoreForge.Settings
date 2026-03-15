using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using KF.Settings.Configuration;
using KF.Settings.Core.Services;
using KF.Settings.Data;
using KF.Settings.Data.Entities;
using KF.Settings.Errors;
using KF.Settings.Interfaces;
using KF.Settings.Metrics;
using KF.Settings.Models;
using KF.Settings.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KF.Settings.Reload;

public sealed class SettingsReloadBackgroundService : BackgroundService
{
    private readonly ILogger<SettingsReloadBackgroundService> _logger;
    private readonly IMetricsRecorder _metrics;
    private readonly IDbContextFactory<KFSettingsDbContext> _dbFactory;
    private readonly KFSettingsOptions _options;
    private readonly KFSettingsConfigurationProvider _provider;
    private readonly HealthReporter _health;
    private readonly BinarySettingsAccessor? _binary;
    private long _lastRowCount;
    private byte[]? _lastMaxRv;
    private int _lastKeyChecksum;
    private byte[]? _lastScopeHash;
    private int _consecutiveFailures;

    public SettingsReloadBackgroundService(
        ILogger<SettingsReloadBackgroundService> logger,
        IMetricsRecorder metrics,
        IDbContextFactory<KFSettingsDbContext> dbFactory,
        KFSettingsOptions options,
        KFSettingsConfigurationProvider provider,
        HealthReporter health,
        BinarySettingsAccessor? binary = null)
    {
        _logger = logger;
        _metrics = metrics;
        _dbFactory = dbFactory;
        _options = options;
        _provider = provider;
        _health = health;
        _binary = binary;
        if (_options.PollingInterval < TimeSpan.FromSeconds(30))
            _options.PollingInterval = TimeSpan.FromSeconds(30);
        if (_options.PollingInterval < TimeSpan.FromMinutes(1))
            _logger.LogWarning("Polling interval below recommended 60s: {Interval}s", _options.PollingInterval.TotalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KF Settings reload service started. Interval={Interval}s", _options.PollingInterval.TotalSeconds);
        await SafeReload(true, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(_options.PollingInterval, stoppingToken); }
            catch { break; }
            await SafeReload(false, stoppingToken);
        }
    }

    private async Task SafeReload(bool coldStart, CancellationToken ct)
    {
        try
        {
            if (!await DetectChanges(ct)) { _metrics.Increment(MetricsNames.ReloadSkipped); return; }
            await BuildSnapshot(ct);
            _metrics.Increment(MetricsNames.ReloadSuccess);
            _consecutiveFailures = 0;
            _health.LastSuccessfulReloadUtc = DateTime.UtcNow;
        }
        catch (ValidationFailureException vfe)
        {
            _metrics.Increment(MetricsNames.ValidationFailure);
            _logger.LogWarning("Validation failure on reload: {Msg}", vfe.Message);
            if (coldStart && _options.FailFastOnStartup) throw;
        }
        catch (Exception ex)
        {
            _metrics.Increment(MetricsNames.ReloadFailure);
            _consecutiveFailures++;
            _health.ConsecutiveFailures = _consecutiveFailures;
            _metrics.SetGauge(MetricsNames.PollFailuresConsecutive, _consecutiveFailures);
            if (_consecutiveFailures == 1) _logger.LogWarning(ex, "Settings reload failed (count={Count})", _consecutiveFailures);
            else _logger.LogError(ex, "Settings reload failed (count={Count})", _consecutiveFailures);
            if (coldStart && _options.FailFastOnStartup) throw;
        }
    }

    private async Task<bool> DetectChanges(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var predicate = InScope();
        var scope = db.Settings.AsNoTracking().Where(predicate);
        var list = await scope.Select(s => new { s.Key, s.RowVersion }).ToListAsync(ct);
        long rowCount = list.LongCount();
        byte[]? maxRv = list.OrderByDescending(r => r.RowVersion, ByteArrayComparer.Instance).FirstOrDefault()?.RowVersion;
        int keyChecksum = 0;
        unchecked { foreach (var k in list) keyChecksum += k.Key.GetHashCode(StringComparison.OrdinalIgnoreCase); }
        bool fastChanged = rowCount != _lastRowCount || !ByteArrayComparer.EqualsStatic(maxRv, _lastMaxRv) || keyChecksum != _lastKeyChecksum;
        if (!fastChanged) return false;
        var full = await scope.Select(s => new { s.Key, s.Value, s.BinaryValue, s.ModifiedDate, s.IsSecret, s.ValueEncrypted, s.RowVersion }).ToListAsync(ct);
        using var sha = SHA256.Create();
        var sb = new StringBuilder();
        foreach (var r in full.OrderBy(r => r.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(r.Key).Append('|')
              .Append(r.Value ?? "").Append('|')
              .Append(r.BinaryValue is null ? string.Empty : Convert.ToHexString(SHA256.HashData(r.BinaryValue))).Append('|')
              .Append(r.ModifiedDate.ToUniversalTime().ToString("O")).Append('|')
              .Append(r.IsSecret ? '1' : '0').Append('|')
              .Append(r.ValueEncrypted ? '1' : '0').Append('|')
              .Append(Convert.ToHexString(r.RowVersion)).Append('~');
        }
        var scopeHash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        bool changed = !ByteArrayComparer.EqualsStatic(scopeHash, _lastScopeHash);
        if (changed)
        {
            _lastRowCount = rowCount; _lastMaxRv = maxRv; _lastKeyChecksum = keyChecksum; _lastScopeHash = scopeHash;
            _health.LastRowCount = rowCount;
            _health.LastHashSnippet = Convert.ToHexString(scopeHash)[..8];
        }
        return changed;
    }

    private async Task BuildSnapshot(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var predicate = InScope();
        var rows = await db.Settings.AsNoTracking().Where(predicate).ToListAsync(ct);
        var ordered = rows.OrderBy(r => r.ApplicationId != null ? 1 : 0).ThenBy(r => r.InstanceId != null ? 1 : 0);
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var bin = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in ordered)
        {
            if (r.Value is not null)
                dict[r.Key] = r.Value;
            else if (r.BinaryValue is not null)
                bin[r.Key] = r.BinaryValue.ToArray();
        }
        _provider.Publish(dict);
        _binary?.SetSnapshot(bin.ToImmutable());
        _logger.LogInformation("Settings snapshot published: keys={Count} binaries={BinCount}", dict.Count, bin.Count);
    }

    private Expression<Func<SettingEntity, bool>> InScope() =>
        s => (s.ApplicationId == null && s.InstanceId == null) ||
             (s.ApplicationId == _options.ApplicationId && s.InstanceId == null) ||
             (_options.InstanceId != null && s.ApplicationId == _options.ApplicationId && s.InstanceId == _options.InstanceId);

    private sealed class ByteArrayComparer : IComparer<byte[]?>
    {
        public static readonly ByteArrayComparer Instance = new();
        public int Compare(byte[]? x, byte[]? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            for (int i = 0; i < Math.Min(x.Length, y.Length); i++) { int c = x[i].CompareTo(y[i]); if (c != 0) return c; }
            return x.Length.CompareTo(y.Length);
        }
        public static bool EqualsStatic(byte[]? x, byte[]? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }
    }
}
