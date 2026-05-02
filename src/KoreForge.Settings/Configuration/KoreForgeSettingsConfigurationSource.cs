using Microsoft.Extensions.Configuration;

namespace KoreForge.Settings.Configuration;

public sealed class KoreForgeSettingsConfigurationSource : IConfigurationSource
{
    public KoreForgeSettingsConfigurationProvider Provider { get; } = new();
    public IConfigurationProvider Build(IConfigurationBuilder builder) => Provider;
}
