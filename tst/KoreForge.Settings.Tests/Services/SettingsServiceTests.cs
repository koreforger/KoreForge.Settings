using FluentAssertions;
using KoreForge.Settings.Core.Services;
using KoreForge.Settings.Errors;
using KoreForge.Settings.Models;
using KoreForge.Settings.Tests.Helpers;
using KoreForge.Time;
using Microsoft.EntityFrameworkCore;

namespace KoreForge.Settings.Tests.Services;

public class SettingsServiceTests
{
    private static SettingsService Create(out InMemoryDbContextFactory factory, out TestMetricsRecorder metrics)
    { factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N")); metrics = new TestMetricsRecorder(); return new SettingsService(factory, metrics, UtcSystemClock.Instance); }

    [Fact]
    public async Task Given_NewSetting_When_UpsertText_Then_CreatesAndHistoryRecorded()
    {
        var svc = Create(out var f, out var metrics);
        var created = await svc.UpsertAsync(new SettingUpsert { Key = "A:B", Value = "1", ChangedBy = "u" }, CancellationToken.None);
        created.Value.Should().Be("1");
        var db = f.CreateDbContext();
        (await db.SettingsHistory.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Given_NewSetting_When_ValueAndBinaryBothProvided_Then_ValidationFailure()
    {
        var svc = Create(out _, out _);
        await FluentActions.Invoking(() => svc.UpsertAsync(new SettingUpsert { Key = "K", Value = "v", BinaryValue = new byte[] { 1 }, ChangedBy = "u" }, CancellationToken.None))
            .Should().ThrowAsync<ValidationFailureException>();
    }

    [Fact]
    public async Task Given_ExistingSetting_When_UpdateWithoutRowVersion_Then_MissingRowVersion()
    {
        var svc = Create(out var f, out _);
        var created = await svc.UpsertAsync(new SettingUpsert { Key = "K", Value = "v", ChangedBy = "u" }, CancellationToken.None);
        await FluentActions.Invoking(() => svc.UpsertAsync(new SettingUpsert { Key = created.Key, Value = "v2", ChangedBy = "u" }, CancellationToken.None))
            .Should().ThrowAsync<MissingRowVersionException>();
    }

    [Fact]
    public async Task Given_ExistingSetting_When_UpdateWithStaleRowVersion_Then_ConcurrencyConflict()
    {
        var svc = Create(out var f, out _);
        var created = await svc.UpsertAsync(new SettingUpsert { Key = "K", Value = "v", ChangedBy = "u" }, CancellationToken.None);
        var stale = created.RowVersion.ToArray();
        // Manually mutate the stored rowversion to simulate concurrent change (InMemory does not auto-update [Timestamp])
        var manualCtx = f.CreateDbContext();
        var entity = await manualCtx.Settings.FirstAsync();
        entity.Value = "v2"; // change something
        entity.RowVersion = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2 }; // force different rowversion
        await manualCtx.SaveChangesAsync();
        await FluentActions.Invoking(() => svc.UpsertAsync(new SettingUpsert { Key = "K", Value = "v3", ChangedBy = "u", ExpectedRowVersion = stale }, CancellationToken.None))
            .Should().ThrowAsync<ConcurrencyConflictException>();
    }

    [Fact]
    public async Task Given_ExistingSetting_When_DeleteWithCorrectRowVersion_Then_RemovedAndHistoryAdded()
    {
        var svc = Create(out var f, out _);
        var created = await svc.UpsertAsync(new SettingUpsert { Key = "K", Value = "v", ChangedBy = "u" }, CancellationToken.None);
        await svc.DeleteAsync(created.Id, "u", created.RowVersion, CancellationToken.None);
        var db = f.CreateDbContext();
        (await db.Settings.CountAsync()).Should().Be(0);
        (await db.SettingsHistory.CountAsync()).Should().Be(2); // insert + delete
    }

    [Fact]
    public async Task Given_SameKeyDifferentClientVersions_When_UpsertBoth_Then_BothExistIndependently()
    {
        var svc = Create(out var f, out _);
        var legacy = await svc.UpsertAsync(new SettingUpsert { Key = "BatchSize", Value = "50", ApplicationId = "App1", ChangedBy = "u" }, CancellationToken.None);
        var next = await svc.UpsertAsync(new SettingUpsert { Key = "BatchSize", Value = "100", ApplicationId = "App1", ClientAppVersion = "v2.0.0", ChangedBy = "u" }, CancellationToken.None);
        legacy.ClientAppVersion.Should().BeNull();
        next.ClientAppVersion.Should().Be("v2.0.0");
        var db = f.CreateDbContext();
        (await db.Settings.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Given_SameKeyAndClientVersion_When_UpdatedWithCorrectRowVersion_Then_VersionScopePreserved()
    {
        var svc = Create(out _, out _);
        var created = await svc.UpsertAsync(new SettingUpsert { Key = "K", Value = "old", ApplicationId = "App1", ClientAppVersion = "v2.0.0", ChangedBy = "u" }, CancellationToken.None);
        var updated = await svc.UpsertAsync(new SettingUpsert { Key = "K", Value = "new", ApplicationId = "App1", ClientAppVersion = "v2.0.0", ChangedBy = "u", ExpectedRowVersion = created.RowVersion }, CancellationToken.None);
        updated.Value.Should().Be("new");
        updated.ClientAppVersion.Should().Be("v2.0.0");
    }

    [Fact]
    public async Task Given_VersionScopeSetting_When_QueryFilteredByVersion_Then_OnlyMatchingReturned()
    {
        var svc = Create(out _, out _);
        await svc.UpsertAsync(new SettingUpsert { Key = "K", Value = "legacy", ApplicationId = "App1", ChangedBy = "u" }, CancellationToken.None);
        await svc.UpsertAsync(new SettingUpsert { Key = "K", Value = "v2val", ApplicationId = "App1", ClientAppVersion = "v2.0.0", ChangedBy = "u" }, CancellationToken.None);
        var v2Rows = await svc.QueryAsync(new SettingQuery { ApplicationId = "App1", ClientAppVersion = "v2.0.0" }, CancellationToken.None);
        v2Rows.Should().HaveCount(1);
        v2Rows.Single().Value.Should().Be("v2val");
    }
}
