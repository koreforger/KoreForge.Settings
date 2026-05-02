namespace KoreForge.Settings.Interfaces;

public interface ISettingsSnapshotSource
{
    IReadOnlyDictionary<string, string?> CurrentValues { get; }
}
