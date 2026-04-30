using FluentAssertions;
using KF.Settings.Core.Services;
using KF.Settings.Errors;
using KF.Settings.Models;
using KF.Settings.Tests.Helpers;
using KF.Time;
using Microsoft.EntityFrameworkCore;

namespace KF.Settings.Tests.Services;

public class HistoryServiceAdvancedTests
{
    [Fact]
    public async Task Given_RowDeleted_When_RollbackDelete_Then_RowRestored()
    {
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));
        var metrics = new TestMetricsRecorder();
        var svc = new SettingsService(factory, metrics, UtcSystemClock.Instance);
        var histSvc = new HistoryService(factory);
        var created = await svc.UpsertAsync(new SettingUpsert { Key = "DelKey", Value = "v", ChangedBy = "u" }, CancellationToken.None);
        await svc.DeleteAsync(created.Id, "u", created.RowVersion, CancellationToken.None);
            await histSvc.RollbackAsync("DelKey", null, null, null, 0, "u", CancellationToken.None);
        var rows = await svc.QueryAsync(new SettingQuery { KeyPrefix = "DelKey" }, CancellationToken.None);
        rows.Should().HaveCount(1);
        rows.Single().Value.Should().Be("v");
    }

    [Fact]
    public async Task Given_HistoryIndexOutOfRange_When_Rollback_Then_ArgumentOutOfRange()
    {
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));
        var metrics = new TestMetricsRecorder();
        var svc = new SettingsService(factory, metrics, UtcSystemClock.Instance);
        var histSvc = new HistoryService(factory);
        await svc.UpsertAsync(new SettingUpsert { Key = "Idx", Value = "v", ChangedBy = "u" }, CancellationToken.None);
        await FluentActions.Invoking(() => histSvc.RollbackAsync("Idx", null, null, null, 99, "u", CancellationToken.None))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Given_ModifiedAfterUpdate_When_RollbackOlderUpdate_Then_RollbackConflict()
    {
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));
        var metrics = new TestMetricsRecorder();
        var svc = new SettingsService(factory, metrics, UtcSystemClock.Instance);
        var histSvc = new HistoryService(factory);
        var v1 = await svc.UpsertAsync(new SettingUpsert { Key = "RK", Value = "1", ChangedBy = "u" }, CancellationToken.None);
        var v2 = await svc.UpsertAsync(new SettingUpsert { Key = "RK", Value = "2", ChangedBy = "u", ExpectedRowVersion = v1.RowVersion }, CancellationToken.None);
        var v3 = await svc.UpsertAsync(new SettingUpsert { Key = "RK", Value = "3", ChangedBy = "u", ExpectedRowVersion = v2.RowVersion }, CancellationToken.None);
        // InMemory does not auto-change RowVersion; mutate manually to simulate concurrency after v3
        var ctx = factory.CreateDbContext();
        var entity = await ctx.Settings.FirstAsync();
        entity.RowVersion = new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 };
        await ctx.SaveChangesAsync();
        // history order: v3(update), v2(update), v1(insert) indexes 0,1,2; rolling back index 1 should now detect mismatch
            await FluentActions.Invoking(() => histSvc.RollbackAsync("RK", null, null, null, 1, "u", CancellationToken.None))
            .Should().ThrowAsync<RollbackConflictException>();
    }
}
