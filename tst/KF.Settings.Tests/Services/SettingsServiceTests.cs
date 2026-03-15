using FluentAssertions;
using KF.Settings.Core.Services;
using KF.Settings.Errors;
using KF.Settings.Models;
using KF.Settings.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace KF.Settings.Tests.Services;

public class SettingsServiceTests
{
    private static SettingsService Create(out InMemoryDbContextFactory factory, out TestMetricsRecorder metrics)
    { factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N")); metrics = new TestMetricsRecorder(); return new SettingsService(factory, metrics); }

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
}
