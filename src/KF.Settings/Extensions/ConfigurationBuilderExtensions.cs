using KF.Settings.Configuration;
using KF.Settings.Core.Services;
using KF.Settings.Data;
using KF.Settings.Encryption;
using KF.Settings.Interfaces;
using KF.Settings.Metrics;
using KF.Settings.Options;
using KF.Settings.Reload;
using KF.Settings.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace KF.Settings.Extensions;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddKFSettings(this IConfigurationBuilder builder, Action<KFSettingsOptions>? configure = null)
    {
        var opts = new KFSettingsOptions();
        configure?.Invoke(opts);
        var source = new KFSettingsConfigurationSource();
        builder.Add(source);
        builder.Properties[nameof(KFSettingsOptions)] = (opts, source);
        return builder;
    }

    private static string? ResolveConnectionString(IConfiguration config, KFSettingsOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.ConnectionString)) return opts.ConnectionString;
        var fromConfig = config["KF:Settings:ConnectionString"] ?? config["ConnectionStrings:KFSettings"];
        if (!string.IsNullOrWhiteSpace(fromConfig)) return fromConfig;
        var fromEnv = Environment.GetEnvironmentVariable("KF_SETTINGS_CONNECTIONSTRING");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;
        return null;
    }

    public static IServiceCollection AddKFSettingsServices(this IServiceCollection services, IConfiguration configuration)
    {
        KFSettingsOptions opts;
        KFSettingsConfigurationSource? src = null;
        if (configuration is IConfigurationBuilder cb &&
            cb.Properties.TryGetValue(nameof(KFSettingsOptions), out var stored) &&
            stored is ValueTuple<KFSettingsOptions, KFSettingsConfigurationSource> tuple)
        {
            opts = tuple.Item1;
            src = tuple.Item2;
        }
        else
        {
            opts = new KFSettingsOptions();
        }
        opts.ConnectionString = ResolveConnectionString(configuration, opts)
            ?? throw new InvalidOperationException("KF Settings connection string not resolved. Set ConnectionStrings:KFSettings, KF:Settings:ConnectionString, or KF_SETTINGS_CONNECTIONSTRING env var.");

        services.TryAddSingleton(opts);
        if (src != null) services.TryAddSingleton(src.Provider);
        services.AddSingleton<IValidateOptions<KFSettingsOptions>, OptionsValidator>();
        if (opts.EnableDecryption && services.All(d => d.ServiceType != typeof(IEncryptionProvider)))
            throw new InvalidOperationException("EnableDecryption=true but no IEncryptionProvider is registered.");
        services.TryAddSingleton<IEncryptionProvider, NoOpEncryptionProvider>();
        services.TryAddSingleton<IMetricsRecorder>(sp => opts.EnableMetrics ? new InMemoryMetricsRecorder() : new NoOpMetricsRecorder());
        services.AddDbContextFactory<KFSettingsDbContext>(o => o.UseSqlServer(opts.ConnectionString));
        services.TryAddScoped<ISettingsService, SettingsService>();
        services.TryAddScoped<IHistoryService, HistoryService>();
        services.TryAddSingleton<BinarySettingsAccessor>();
        services.TryAddSingleton<IBinarySettingsAccessor>(sp => sp.GetRequiredService<BinarySettingsAccessor>());
        services.TryAddSingleton<IHealthReporter, HealthReporter>();
        services.AddHostedService<SettingsReloadBackgroundService>();
        return services;
    }
}
