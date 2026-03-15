using FluentAssertions;
using KF.Settings.Core.Services;
using KF.Settings.Models;
using KF.Settings.Tests.Helpers;

namespace KF.Settings.Tests.Services;

public class HistoryServiceTests
{
    [Fact]
    public async Task Given_UpdateSequence_When_RollbackRequested_Then_ValueRestoredAndRollbackHistoryAdded()
    {
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));
        var metrics = new TestMetricsRecorder();
        var settings = new SettingsService(factory, metrics);
        var historySvc = new HistoryService(factory);
        var created = await settings.UpsertAsync(new SettingUpsert { Key = "X", Value = "1", ChangedBy = "u" }, CancellationToken.None);
        var updated = await settings.UpsertAsync(new SettingUpsert { Key = "X", Value = "2", ChangedBy = "u", ExpectedRowVersion = created.RowVersion }, CancellationToken.None);
        // Roll back the last update (newest history entry = index 0)
        await historySvc.RollbackAsync("X", 0, "u", CancellationToken.None);
        var again = await settings.QueryAsync(new SettingQuery { KeyPrefix = "X" }, CancellationToken.None);
        again.Single().Value.Should().Be("1");
    }
}
