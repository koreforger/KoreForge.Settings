using System.Collections.Concurrent;
using KF.Settings.Interfaces;

namespace KF.Settings.Metrics;

public static class MetricsNames
{
    public const string ReloadSuccess = "kf_settings_reload_success_total";
    public const string ReloadSkipped = "kf_settings_reload_skipped_total";
    public const string ReloadFailure = "kf_settings_reload_failure_total";
    public const string ValidationFailure = "kf_settings_validation_failure_total";
    public const string ConcurrencyConflict = "kf_settings_reload_concurrency_conflict_total";
    public const string PollFailuresConsecutive = "kf_settings_poll_failures_consecutive";
}

public sealed class NoOpMetricsRecorder : IMetricsRecorder
{
    private sealed class NoOpDisp : IDisposable
    {
        public void Dispose() { }
    }

    public void Increment(string name, long value = 1) { }
    public void SetGauge(string name, double value) { }
    public IDisposable Time(string name) => new NoOpDisp();
}

public sealed class InMemoryMetricsRecorder : IMetricsRecorder
{
    private readonly ConcurrentDictionary<string, long> _counters = new();
    private readonly ConcurrentDictionary<string, double> _gauges = new();

    public void Increment(string name, long value = 1) =>
        _counters.AddOrUpdate(name, value, (_, existing) => existing + value);

    public void SetGauge(string name, double value) => _gauges[name] = value;

    public IDisposable Time(string name)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        return new StopwatchOwner(sw, elapsed => Increment($"{name}:ms", (long)elapsed.TotalMilliseconds));
    }

    private sealed class StopwatchOwner(System.Diagnostics.Stopwatch sw, Action<TimeSpan> report) : IDisposable
    {
        public void Dispose()
        {
            sw.Stop();
            report(sw.Elapsed);
        }
    }

    public IReadOnlyDictionary<string, long> SnapshotCounters() =>
        new Dictionary<string, long>(_counters);

    public IReadOnlyDictionary<string, double> SnapshotGauges() =>
        new Dictionary<string, double>(_gauges);
}
