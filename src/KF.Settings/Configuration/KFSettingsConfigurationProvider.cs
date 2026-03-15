using KF.Settings.Interfaces;
using Microsoft.Extensions.Configuration;

namespace KF.Settings.Configuration;

public sealed class KFSettingsConfigurationProvider : ConfigurationProvider, IDisposable, ISettingsSnapshotSource
{
    private readonly object _sync = new();
    private bool _active = true;
    private CancellationTokenSource _cts = new();

    public IReadOnlyDictionary<string, string?> CurrentValues => new Dictionary<string, string?>(Data);

    public override void Load() { }

    public void Publish(IDictionary<string, string?> values)
    {
        lock (_sync)
        {
            Data = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
            var old = _cts; _cts = new(); old.Cancel(); old.Dispose();
        }
        OnReload();
    }

    public void Dispose()
    {
        if (!_active) return;
        _active = false;
        _cts.Cancel();
        _cts.Dispose();
    }
}
