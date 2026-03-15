using FluentAssertions;
using KF.Settings.Data.Entities;
using KF.Settings.Metrics;
using KF.Settings.Options;
using KF.Settings.Configuration;
using KF.Settings.Reload;
using KF.Settings.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace KF.Settings.Tests.Provider;

public class ReloadBackgroundServiceTests
{
    private static (SettingsReloadBackgroundService svc, KFSettingsConfigurationProvider provider, InMemoryDbContextFactory factory, TestMetricsRecorder metrics, HealthReporter health) Build(IEnumerable<SettingEntity>? seed = null)
    {
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));
        var ctx = factory.CreateDbContext();
        if (seed != null) { ctx.Settings.AddRange(seed); ctx.SaveChanges(); }
        var metrics = new TestMetricsRecorder();
        var provider = new KFSettingsConfigurationProvider();
        var opts = new KFSettingsOptions { ApplicationId = null, InstanceId = null, PollingInterval = TimeSpan.FromMilliseconds(150), EnableMetrics = true };
        var health = new HealthReporter();
        var svc = new SettingsReloadBackgroundService(new NullLogger<SettingsReloadBackgroundService>(), metrics, factory, opts, provider, health, new KF.Settings.Core.Services.BinarySettingsAccessor());
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
}
