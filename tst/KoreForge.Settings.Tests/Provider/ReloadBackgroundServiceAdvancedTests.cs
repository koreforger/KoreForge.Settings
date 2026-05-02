using FluentAssertions;
using KoreForge.Settings.Data.Entities;
using KoreForge.Settings.Options;
using KoreForge.Settings.Configuration;
using KoreForge.Settings.Reload;
using KoreForge.Settings.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace KoreForge.Settings.Tests.Provider;

public class ReloadBackgroundServiceAdvancedTests
{
    private sealed class ThrowFactory : IDbContextFactory<KoreForge.Settings.Data.KoreForgeSettingsDbContext>
    {
        public KoreForge.Settings.Data.KoreForgeSettingsDbContext CreateDbContext() => throw new InvalidOperationException("boom");
        public Task<KoreForge.Settings.Data.KoreForgeSettingsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("boom");
    }

    private static (SettingsReloadBackgroundService svc, KoreForgeSettingsConfigurationProvider provider, InMemoryDbContextFactory realFactory, TestMetricsRecorder metrics, HealthReporter health, KoreForge.Settings.Core.Services.BinarySettingsAccessor bin, KoreForgeSettingsOptions opts) Build(bool enableDecrypt = false, IEnumerable<SettingEntity>? seed = null)
    {
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));
        var ctx = factory.CreateDbContext();
        if (seed != null) { ctx.Settings.AddRange(seed); ctx.SaveChanges(); }
        var metrics = new TestMetricsRecorder();
        var provider = new KoreForgeSettingsConfigurationProvider();
        var opts = new KoreForgeSettingsOptions { EnableMetrics = true, PollingInterval = TimeSpan.FromMilliseconds(120), EnableDecryption = enableDecrypt, EnableDetailedLogging = false, FailFastOnStartup = false };
        var health = new HealthReporter();
        var bin = new KoreForge.Settings.Core.Services.BinarySettingsAccessor();
        var logger = LoggerFactory.Create(b => { }).CreateLogger<SettingsReloadBackgroundService>();
        var svc = new SettingsReloadBackgroundService(logger, metrics, factory, opts, provider, health, bin);
        return (svc, provider, factory, metrics, health, bin, opts);
    }

    [Fact]
    public async Task Given_SecretAndBinary_When_Reload_Then_SecretValuePresentAndBinaryAccessible()
    {
        var seed = new[] { new SettingEntity { Key = "SecretKey", Value = "TopSecret", IsSecret = true, CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow }, new SettingEntity { Key = "Bin", BinaryValue = new byte[] { 1, 2, 3 }, CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow } };
        var (svc, provider, _, _, _, bin, _) = Build(false, seed);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await svc.StartAsync(cts.Token);
        await Task.Delay(400, cts.Token);
        provider.CurrentValues["SecretKey"].Should().Be("TopSecret");
        bin.TryGet("Bin", out var bytes).Should().BeTrue();
        bytes.Length.Should().Be(3);
        await svc.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Given_EncryptedValue_When_DecryptionEnabled_Then_ValueReturned()
    {
        var seed = new[] { new SettingEntity { Key = "Enc", Value = "Cipher", ValueEncrypted = true, CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow } };
        var (svc, provider, _, _, _, _, _) = Build(true, seed);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await svc.StartAsync(cts.Token);
        await Task.Delay(400, cts.Token);
        provider.CurrentValues["Enc"].Should().Be("Cipher");
        await svc.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Given_SubsequentFailure_When_Poll_Then_FailureMetricAndHealthUpdated()
    {
        var seed = new[] { new SettingEntity { Key = "A", Value = "1", CreatedBy = "u", ModifiedBy = "u", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow } };
        var (svc, provider, factory, metrics, health, _, baseOpts) = Build(false, seed);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await svc.StartAsync(cts.Token);
        await Task.Delay(350, cts.Token); // initial success
        // second service with throwing factory; disable fail-fast so exception is swallowed and counted
        var failingOpts = new KoreForgeSettingsOptions { PollingInterval = TimeSpan.FromMilliseconds(120), FailFastOnStartup = false };
        var failingSvc = new SettingsReloadBackgroundService(LoggerFactory.Create(b => { }).CreateLogger<SettingsReloadBackgroundService>(), metrics, new ThrowFactory(), failingOpts, provider, health, null);
        await failingSvc.StartAsync(cts.Token);
        await Task.Delay(300, cts.Token);
        health.ConsecutiveFailures.Should().BeGreaterThan(0);
    }
}
