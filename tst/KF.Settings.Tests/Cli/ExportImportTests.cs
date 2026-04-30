using FluentAssertions;
using KF.Settings.Cli.Commands;
using KF.Settings.Core.Services;
using KF.Settings.Data;
using KF.Settings.Models;
using KF.Settings.Options;
using KF.Settings.Tests.Helpers;
using KF.Settings.Interfaces;
using KF.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace KF.Settings.Tests.Cli;

public class ExportImportTests
{
    private RootServices Build()
    {
        var services = new ServiceCollection();
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));
        services.AddSingleton<IDbContextFactory<KFSettingsDbContext>>(factory);
        services.AddSingleton<IMetricsRecorder, KF.Settings.Metrics.NoOpMetricsRecorder>();
        services.AddSingleton<ISystemClock>(UtcSystemClock.Instance);
        services.AddSingleton<ISettingsService, SettingsService>();
        var opts = new KFSettingsOptions();
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
