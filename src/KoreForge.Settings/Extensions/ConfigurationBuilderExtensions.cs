using KoreForge.Settings.Configuration;
using KoreForge.Settings.Core.Services;
using KoreForge.Settings.Data;
using KoreForge.Settings.Encryption;
using KoreForge.Settings.Interfaces;
using KoreForge.Settings.Metrics;
using KoreForge.Settings.Options;
using KoreForge.Settings.Reload;
using KoreForge.Settings.Validation;
using KoreForge.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace KoreForge.Settings.Extensions;

public static class ConfigurationBuilderExtensions
{
    private const string SettingsBuilderStateKey = "KoreForge.Settings.BuilderState";

    public static IConfigurationBuilder AddKoreForgeSettings(this IConfigurationBuilder builder, Action<KoreForgeSettingsOptions>? configure = null)
    {
        var opts = new KoreForgeSettingsOptions();
        configure?.Invoke(opts);
        var source = new KoreForgeSettingsConfigurationSource();
        builder.Add(source);
        builder.Properties[SettingsBuilderStateKey] = (opts, source);
        // Backward compatibility for callers that inspect the legacy key.
        builder.Properties[nameof(KoreForgeSettingsOptions)] = (opts, source);
        return builder;
    }

    private static string? ResolveConnectionString(IConfiguration config, KoreForgeSettingsOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.ConnectionString)) return opts.ConnectionString;
        var fromConfig = config["KoreForge:Settings:ConnectionString"] ?? config["ConnectionStrings:KoreForgeSettings"];
        if (!string.IsNullOrWhiteSpace(fromConfig)) return fromConfig;
        var fromEnv = Environment.GetEnvironmentVariable("KOREFORGE_SETTINGS_CONNECTIONSTRING");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;
        return null;
    }

    public static IServiceCollection AddKoreForgeSettingsServices(this IServiceCollection services, IConfiguration configuration)
    {
        KoreForgeSettingsOptions opts;
        KoreForgeSettingsConfigurationSource? src = null;
        if (configuration is IConfigurationBuilder cb &&
            (cb.Properties.TryGetValue(SettingsBuilderStateKey, out var stored) ||
             cb.Properties.TryGetValue(nameof(KoreForgeSettingsOptions), out stored)) &&
            stored is ValueTuple<KoreForgeSettingsOptions, KoreForgeSettingsConfigurationSource> tuple)
        {
            opts = tuple.Item1;
            src = tuple.Item2;
        }
        else
        {
            opts = new KoreForgeSettingsOptions();
        }

        // Populate scope defaults from configuration when AddKoreForgeSettings state is unavailable
        // (for example when only IConfiguration is provided).
        opts.ApplicationId ??= configuration["KoreForgeSettings:ApplicationId"]
            ?? configuration["KoreForge:Settings:Application"];
        opts.InstanceId ??= configuration["KoreForgeSettings:InstanceId"]
            ?? configuration["KoreForge:Settings:Instance"];
        opts.ClientAppVersion ??= configuration["KoreForgeSettings:ClientAppVersion"]
            ?? configuration["KoreForge:Settings:ClientAppVersion"];
        if (opts.PollingInterval == default)
        {
            opts.PollingInterval =
                configuration.GetValue<TimeSpan?>("KoreForgeSettings:PollingInterval")
                ?? configuration.GetValue<TimeSpan?>("KoreForge:Settings:PollingInterval")
                ?? TimeSpan.FromMinutes(1);
        }

        opts.ConnectionString = ResolveConnectionString(configuration, opts)
            ?? throw new InvalidOperationException("KoreForge Settings connection string not resolved. Set ConnectionStrings:KoreForgeSettings, KoreForge:Settings:ConnectionString, or KOREFORGE_SETTINGS_CONNECTIONSTRING env var.");

        services.TryAddSingleton(opts);
        if (src != null) services.TryAddSingleton(src.Provider);
        services.AddSingleton<IValidateOptions<KoreForgeSettingsOptions>, OptionsValidator>();
        if (opts.EnableDecryption && services.All(d => d.ServiceType != typeof(IEncryptionProvider)))
            throw new InvalidOperationException("EnableDecryption=true but no IEncryptionProvider is registered.");
        services.TryAddSingleton<IEncryptionProvider, NoOpEncryptionProvider>();
        services.TryAddSingleton<IMetricsRecorder>(sp => opts.EnableMetrics ? new InMemoryMetricsRecorder() : new NoOpMetricsRecorder());
        services.TryAddSingleton<ISystemClock>(UtcSystemClock.Instance);
        services.AddDbContextFactory<KoreForgeSettingsDbContext>(o => o.UseSqlServer(opts.ConnectionString));
        services.TryAddScoped<ISettingsService, SettingsService>();
        services.TryAddScoped<IHistoryService, HistoryService>();
        services.TryAddSingleton<BinarySettingsAccessor>();
        services.TryAddSingleton<IBinarySettingsAccessor>(sp => sp.GetRequiredService<BinarySettingsAccessor>());
        services.TryAddSingleton<HealthReporter>();
        services.TryAddSingleton<IHealthReporter>(sp => sp.GetRequiredService<HealthReporter>());
        services.AddHostedService<SettingsReloadBackgroundService>();
        return services;
    }
}
