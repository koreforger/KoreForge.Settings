using FluentAssertions;
using KoreForge.Settings.Metrics;

namespace KoreForge.Settings.Tests.Metrics;

public class MetricsRecorderTests
{
    [Fact]
    public void InMemoryMetricsRecorder_TracksCountersGaugesAndTiming()
    {
        var recorder = new InMemoryMetricsRecorder();

        recorder.Increment("requests");
        recorder.Increment("requests", 2);
        recorder.SetGauge("active", 5);
        using (recorder.Time("roundtrip"))
        {
            // dispose immediately; we just care that an entry is recorded
        }

        recorder.SnapshotCounters()["requests"].Should().Be(3);
        recorder.SnapshotGauges()["active"].Should().Be(5);
        recorder.SnapshotCounters().Should().ContainKey("roundtrip:ms");
    }

    [Fact]
    public void NoOpMetricsRecorder_DoesNotThrowAndReturnsDisposable()
    {
        var recorder = new NoOpMetricsRecorder();

        recorder.Invoking(r => r.Increment("anything", 10)).Should().NotThrow();
        recorder.Invoking(r => r.SetGauge("gauge", 1.23)).Should().NotThrow();

        using var timing = recorder.Time("noop");
        timing.Should().NotBeNull();
    }
}
