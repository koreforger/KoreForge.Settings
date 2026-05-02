using FluentAssertions;
using KoreForge.Settings.Cli.Commands;
using KoreForge.Settings.Core.Services;
using KoreForge.Settings.Data;
using KoreForge.Settings.Models;
using KoreForge.Settings.Options;
using KoreForge.Settings.Tests.Helpers;
using KoreForge.Settings.Interfaces;
using KoreForge.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace KoreForge.Settings.Tests.Cli;

public class ExportImportTests
{
    private RootServices Build()
    {
        var services = new ServiceCollection();
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));
        services.AddSingleton<IDbContextFactory<KoreForgeSettingsDbContext>>(factory);
        services.AddSingleton<IMetricsRecorder, KoreForge.Settings.Metrics.NoOpMetricsRecorder>();
        services.AddSingleton<ISystemClock>(UtcSystemClock.Instance);
        services.AddSingleton<ISettingsService, SettingsService>();
        var opts = new KoreForgeSettingsOptions();
        services.AddSingleton(opts);
        var sp = services.BuildServiceProvider();
        return new RootServices(sp, opts);
    }

    [Fact]
    public async Task Given_Settings_When_ExportWithoutSecrets_Then_SecretValueOmitted()
    {
        var rs = Build();
        var svc = rs.Provider.GetRequiredService<ISettingsService>();
        await svc.UpsertAsync(new SettingUpsert { Key = "Public", Value = "P", ChangedBy = "u" }, CancellationToken.None);
        await svc.UpsertAsync(new SettingUpsert { Key = "Secret", Value = "S", ChangedBy = "u", IsSecret = true }, CancellationToken.None);
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var cmd = ExportImport.CreateExport(_ => rs);
        var exit = await cmd.InvokeAsync(new[] { "--file", tmp });
        exit.Should().Be(0);
        var json = await File.ReadAllTextAsync(tmp);
        json.Should().Contain("Public").And.Contain("Secret");
        json.Should().NotContain("\"S\"");
    }
}
