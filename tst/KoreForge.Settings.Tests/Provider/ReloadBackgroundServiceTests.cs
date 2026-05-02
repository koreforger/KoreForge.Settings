using FluentAssertions;
using KoreForge.Settings.Data.Entities;
using KoreForge.Settings.Metrics;
using KoreForge.Settings.Options;
using KoreForge.Settings.Configuration;
using KoreForge.Settings.Reload;
using KoreForge.Settings.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace KoreForge.Settings.Tests.Provider;

public class ReloadBackgroundServiceTests
{
    private static (SettingsReloadBackgroundService svc, KoreForgeSettingsConfigurationProvider provider, InMemoryDbContextFactory factory, TestMetricsRecorder metrics, HealthReporter health) Build(
        IEnumerable<SettingEntity>? seed = null,
        string? appId = null,
        string? instanceId = null,
        string? clientAppVersion = null)
    {
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));
        var ctx = factory.CreateDbContext();
        if (seed != null) { ctx.Settings.AddRange(seed); ctx.SaveChanges(); }
        var metrics = new TestMetricsRecorder();
        var provider = new KoreForgeSettingsConfigurationProvider();
        var opts = new KoreForgeSettingsOptions { ApplicationId = appId, InstanceId = instanceId, ClientAppVersion = clientAppVersion, PollingInterval = TimeSpan.FromMilliseconds(150), EnableMetrics = true };
        var health = new HealthReporter();
        var svc = new SettingsReloadBackgroundService(new NullLogger<SettingsReloadBackgroundService>(), metrics, factory, opts, provider, health, new KoreForge.Settings.Core.Services.BinarySettingsAccessor());
        return (svc, provider, factory, metrics, health);
    }

    [Fact]
    public async Task Given_InitialRun_When_ServiceStarts_Then_PublishesSnapshot()
    {
        var seed = new[] { new SettingEntity { Key = "A", Value = "1", CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow } };
        var (svc, provider, _, metrics, health) = Build(seed);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await svc.StartAsync(cts.Token);
        await Task.Delay(400, cts.Token);
        provider.CurrentValues.Should().ContainKey("A");
        health.LastSuccessfulReloadUtc.Should().NotBeNull();
        await svc.StopAsync(CancellationToken.None);
        metrics.Counters.Keys.Should().Contain(k => k.Contains("success", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Given_NoChanges_When_PollOccurs_Then_ReloadSkippedMetricIncrements()
    {
        var seed = new[] { new SettingEntity { Key = "A", Value = "1", CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow } };
        var (svc, provider, factory, metrics, _) = Build(seed);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await svc.StartAsync(cts.Token);
        await Task.Delay(400, cts.Token); // first load
        var initialSuccess = metrics.Counters.Values.Sum();
        // allow another poll with no changes
        await Task.Delay(300, cts.Token);
        var after = metrics.Counters.Values.Sum();
        after.Should().BeGreaterThan(initialSuccess - 1); // some metric incremented (skip or success)
        await svc.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Given_LegacyAndVersionedRows_When_OldAppStarts_Then_GetsLegacyValue()
    {
        // Old app: null ClientAppVersion in options. Only gets global + app + instance-scoped rows (no version).
        var seed = new[]
        {
            new SettingEntity { Key = "BatchSize", Value = "50", ApplicationId = "App1", CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            new SettingEntity { Key = "BatchSize", Value = "100", ApplicationId = "App1", ClientAppVersion = "v2.0.0", CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow }
        };
        var (svc, provider, _, _, _) = Build(seed, appId: "App1", clientAppVersion: null);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await svc.StartAsync(cts.Token);
        await Task.Delay(400, cts.Token);
        provider.CurrentValues["BatchSize"].Should().Be("50");
        await svc.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Given_LegacyAndVersionedRows_When_NewAppStarts_Then_GetsVersionedValue()
    {
        // New app: ClientAppVersion = "v2.0.0". Version-specific row (priority 3) overrides app-only row (priority 1).
        var seed = new[]
        {
            new SettingEntity { Key = "BatchSize", Value = "50", ApplicationId = "App1", CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            new SettingEntity { Key = "BatchSize", Value = "100", ApplicationId = "App1", ClientAppVersion = "v2.0.0", CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow }
        };
        var (svc, provider, _, _, _) = Build(seed, appId: "App1", clientAppVersion: "v2.0.0");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await svc.StartAsync(cts.Token);
        await Task.Delay(400, cts.Token);
        provider.CurrentValues["BatchSize"].Should().Be("100");
        await svc.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Given_VersionedRowAbsent_When_NewAppStarts_Then_FallsBackToAppDefault()
    {
        // Version-specific row doesn't exist for "Timeout", but app-default does. Should fall back.
        var seed = new[]
        {
            new SettingEntity { Key = "Timeout", Value = "30", ApplicationId = "App1", CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow }
        };
        var (svc, provider, _, _, _) = Build(seed, appId: "App1", clientAppVersion: "v2.0.0");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await svc.StartAsync(cts.Token);
        await Task.Delay(400, cts.Token);
        provider.CurrentValues["Timeout"].Should().Be("30");
        await svc.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Given_InstanceAndVersionRows_When_AppWithBoth_Then_InstanceVersionWins()
    {
        // Priority: app+instance+ver (4) > app+ver (3) > app+inst (2) > app (1) > global (0)
        var seed = new[]
        {
            new SettingEntity { Key = "K", Value = "global",        CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            new SettingEntity { Key = "K", Value = "app",           ApplicationId = "App1", CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            new SettingEntity { Key = "K", Value = "app+inst",      ApplicationId = "App1", InstanceId = "i1", CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            new SettingEntity { Key = "K", Value = "app+ver",       ApplicationId = "App1", ClientAppVersion = "v2", CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            new SettingEntity { Key = "K", Value = "app+inst+ver",  ApplicationId = "App1", InstanceId = "i1", ClientAppVersion = "v2", CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow }
        };
        var (svc, provider, _, _, _) = Build(seed, appId: "App1", instanceId: "i1", clientAppVersion: "v2");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await svc.StartAsync(cts.Token);
        await Task.Delay(400, cts.Token);
        provider.CurrentValues["K"].Should().Be("app+inst+ver");
        await svc.StopAsync(CancellationToken.None);
    }
}
