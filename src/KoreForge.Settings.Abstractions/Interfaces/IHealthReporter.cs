namespace KoreForge.Settings.Interfaces;

public interface IHealthReporter
{
    DateTime? LastSuccessfulReloadUtc { get; }
    int ConsecutiveFailures { get; }
    long? LastRowCount { get; }
    string? LastHashSnippet { get; }
}
