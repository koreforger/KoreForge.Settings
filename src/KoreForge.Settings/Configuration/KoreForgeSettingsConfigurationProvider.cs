using KoreForge.Settings.Interfaces;
using Microsoft.Extensions.Configuration;

namespace KoreForge.Settings.Configuration;

public sealed class KoreForgeSettingsConfigurationProvider : ConfigurationProvider, IDisposable, ISettingsSnapshotSource
{
    private readonly object _sync = new();
    private bool _active = true;

    public IReadOnlyDictionary<string, string?> CurrentValues => new Dictionary<string, string?>(Data);

    public override void Load() { }

    public void Publish(IDictionary<string, string?> values)
    {
        lock (_sync)
        {
            Data = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
        }
        OnReload();
    }

    public void Dispose()
    {
        if (!_active) return;
        _active = false;
    }
}
