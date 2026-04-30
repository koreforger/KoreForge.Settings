using FluentAssertions;
using KF.Settings.Core.Services;
using KF.Settings.Errors;
using KF.Settings.Models;
using KF.Settings.Tests.Helpers;
using KF.Time;
using Microsoft.EntityFrameworkCore;

namespace KF.Settings.Tests.Services;

public class SettingsServiceAdvancedTests
{
    private static SettingsService Create(out InMemoryDbContextFactory factory, out TestMetricsRecorder metrics)
    { factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N")); metrics = new TestMetricsRecorder(); return new SettingsService(factory, metrics, UtcSystemClock.Instance); }

    [Fact]
    public async Task Given_BinaryInsert_When_Upsert_Then_BinaryPersistedAndHistoryAdded()
    {
        var svc = Create(out var f, out _);
        var created = await svc.UpsertAsync(new SettingUpsert { Key = "Bin:Cert", BinaryValue = new byte[] { 1, 2, 3 }, ChangedBy = "u", IsSecret = true }, CancellationToken.None);
        created.Value.Should().BeNull();
        created.BinaryValue.Should().NotBeNull();
        var db = f.CreateDbContext();
        (await db.SettingsHistory.CountAsync()).Should().Be(1);
        var hist = await db.SettingsHistory.FirstAsync();
        hist.NewBinaryValue.Should().NotBeNull();
        hist.NewIsSecret.Should().BeTrue();
    }

    [Fact]
    public async Task Given_TextThenBinaryUpdate_When_Upsert_Then_HistoryCapturesOldAndNew()
    {
        var svc = Create(out var f, out _);
        var created = await svc.UpsertAsync(new SettingUpsert { Key = "Swap", Value = "one", ChangedBy = "u" }, CancellationToken.None);
        var updated = await svc.UpsertAsync(new SettingUpsert { Key = "Swap", BinaryValue = new byte[] { 9 }, ChangedBy = "u", ExpectedRowVersion = created.RowVersion }, CancellationToken.None);
        updated.BinaryValue.Should().NotBeNull();
        var db = f.CreateDbContext();
        (await db.SettingsHistory.CountAsync()).Should().Be(2);
        var last = await db.SettingsHistory.OrderBy(h => h.HistoryId).LastAsync();
        last.OldValue.Should().Be("one");
        last.NewBinaryValue.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_BinaryThenTextUpdate_When_Upsert_Then_HistoryShowsBinaryOld()
    {
        var svc = Create(out var f, out _);
        var created = await svc.UpsertAsync(new SettingUpsert { Key = "Swap2", BinaryValue = new byte[] { 1 }, ChangedBy = "u" }, CancellationToken.None);
        var updated = await svc.UpsertAsync(new SettingUpsert { Key = "Swap2", Value = "text", ChangedBy = "u", ExpectedRowVersion = created.RowVersion }, CancellationToken.None);
        updated.Value.Should().Be("text");
        var db = f.CreateDbContext();
        (await db.SettingsHistory.CountAsync()).Should().Be(2);
        var last = await db.SettingsHistory.OrderBy(h => h.HistoryId).LastAsync();
        last.OldBinaryValue.Should().NotBeNull();
        last.NewValue.Should().Be("text");
    }

    [Fact]
    public async Task Given_ExistingRow_When_DeleteWithStaleRowVersion_Then_ConcurrencyConflict()
    {
        var svc = Create(out var f, out _);
        var created = await svc.UpsertAsync(new SettingUpsert { Key = "Del", Value = "v", ChangedBy = "u" }, CancellationToken.None);
        var stale = created.RowVersion.ToArray();
        var ctx = f.CreateDbContext();
        var ent = await ctx.Settings.FirstAsync();
        ent.RowVersion = new byte[] { 5, 4, 3, 2, 1, 0, 0, 1 };
        await ctx.SaveChangesAsync();
        await FluentActions.Invoking(() => svc.DeleteAsync(created.Id, "u", stale, CancellationToken.None))
            .Should().ThrowAsync<ConcurrencyConflictException>();
    }

    [Fact]
    public async Task Given_NonExistingId_When_Delete_Then_NotFound()
    {
        var svc = Create(out _, out _);
        await FluentActions.Invoking(() => svc.DeleteAsync(999, "u", new byte[] { 1 }, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Given_NonExistentRow_When_UpsertWithExpectedRowVersion_Then_MissingRowVersionException()
    {
        var svc = Create(out _, out _);
        await FluentActions.Invoking(() => svc.UpsertAsync(new SettingUpsert { Key = "Ghost", Value = "v", ChangedBy = "u", ExpectedRowVersion = new byte[] { 1, 2 } }, CancellationToken.None))
            .Should().ThrowAsync<MissingRowVersionException>();
    }
}
