using KoreForge.Settings.Interfaces;

namespace KoreForge.Settings.Reload;

public sealed class HealthReporter : IHealthReporter
{
    public DateTime? LastSuccessfulReloadUtc { get; internal set; }
    public int ConsecutiveFailures { get; internal set; }
    public long? LastRowCount { get; internal set; }
    public string? LastHashSnippet { get; internal set; }
}
