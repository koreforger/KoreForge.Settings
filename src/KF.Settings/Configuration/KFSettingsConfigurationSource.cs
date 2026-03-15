using Microsoft.Extensions.Configuration;

namespace KF.Settings.Configuration;

public sealed class KFSettingsConfigurationSource : IConfigurationSource
{
    public KFSettingsConfigurationProvider Provider { get; } = new();
    public IConfigurationProvider Build(IConfigurationBuilder builder) => Provider;
}
