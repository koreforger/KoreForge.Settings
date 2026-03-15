using FluentAssertions;
using KF.Settings.Cli.Commands;
using KF.Settings.Core.Services;
using KF.Settings.Data;
using KF.Settings.Models;
using KF.Settings.Options;
using KF.Settings.Tests.Helpers;
using KF.Settings.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.CommandLine;
using Microsoft.EntityFrameworkCore;

namespace KF.Settings.Tests.Cli;

public class ImportCommandTests
{
    private RootServices Build()
    {
        var services = new ServiceCollection();
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));
        services.AddSingleton<IDbContextFactory<KFSettingsDbContext>>(factory);
        services.AddSingleton<IMetricsRecorder, KF.Settings.Metrics.NoOpMetricsRecorder>();
        services.AddSingleton<ISettingsService, SettingsService>();
        var opts = new KFSettingsOptions();
        services.AddSingleton(opts);
        var sp = services.BuildServiceProvider();
        return new RootServices(sp, opts);
    }

    [Fact]
    public async Task Given_Json_When_ImportDryRun_Then_NoRowsPersisted()
    {
        var rs = Build();
        var cmd = ExportImport.CreateImport(_ => rs);
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var docs = new[] { new ExportImport.ImportDoc("Key1", "Value1", false, null, null) };
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(docs));
        var exit = await cmd.InvokeAsync(new[] { "--file", tmp });
        exit.Should().Be(0);
        var svc = rs.Provider.GetRequiredService<ISettingsService>();
        (await svc.QueryAsync(new SettingQuery { KeyPrefix = "Key1" }, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Json_When_ImportApplyUpsert_Then_RowInserted()
    {
        var rs = Build();
        var cmd = ExportImport.CreateImport(_ => rs);
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var docs = new[] { new ExportImport.ImportDoc("Key2", "Value2", true, null, null) };
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(docs));
        var exit = await cmd.InvokeAsync(new[] { "--file", tmp, "--apply" });
        exit.Should().Be(0);
        var svc = rs.Provider.GetRequiredService<ISettingsService>();
        var rows = await svc.QueryAsync(new SettingQuery { KeyPrefix = "Key2" }, CancellationToken.None);
        rows.Should().HaveCount(1);
        rows.Single().IsSecret.Should().BeTrue();
    }

    [Fact]
    public async Task Given_ExistingRow_When_ImportInsertOnly_Then_SkipsExisting()
    {
        var rs = Build();
        var svc = rs.Provider.GetRequiredService<ISettingsService>();
        await svc.UpsertAsync(new SettingUpsert { Key = "Key3", Value = "A", ChangedBy = "u" }, CancellationToken.None);
        var cmd = ExportImport.CreateImport(_ => rs);
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var docs = new[] { new ExportImport.ImportDoc("Key3", "B", false, null, null) };
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(docs));
        var exit = await cmd.InvokeAsync(new[] { "--file", tmp, "--apply", "--upsert:false" });
        exit.Should().Be(0);
        var rows = await svc.QueryAsync(new SettingQuery { KeyPrefix = "Key3" }, CancellationToken.None);
        rows.Single().Value.Should().Be("A");
    }
}
