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
        await historySvc.RollbackAsync("X", null, null, null, 0, "u", CancellationToken.None);
        var again = await settings.QueryAsync(new SettingQuery { KeyPrefix = "X" }, CancellationToken.None);
        again.Single().Value.Should().Be("1");
    }

    [Fact]
    public async Task Given_TwoVersionScopes_When_RollbackOneVersion_Then_OtherVersionUnaffected()
    {
        var factory = new InMemoryDbContextFactory(Guid.NewGuid().ToString("N"));
        var metrics = new TestMetricsRecorder();
        var settings = new SettingsService(factory, metrics);
        var historySvc = new HistoryService(factory);
        const string app = "MyApp";

        // Insert setting for legacy scope (null client version)
        var legacy = await settings.UpsertAsync(new SettingUpsert { Key = "Threshold", Value = "100", ApplicationId = app, ChangedBy = "u" }, CancellationToken.None);
        // Insert same key for new binary scope (v2.0.0)
        await settings.UpsertAsync(new SettingUpsert { Key = "Threshold", Value = "200", ApplicationId = app, ClientAppVersion = "v2.0.0", ChangedBy = "u" }, CancellationToken.None);

        // Update legacy to "150"
        var legacyUpdated = await settings.UpsertAsync(new SettingUpsert { Key = "Threshold", Value = "150", ApplicationId = app, ChangedBy = "u", ExpectedRowVersion = legacy.RowVersion }, CancellationToken.None);

        // Rollback legacy — should only touch null ClientAppVersion scope
        await historySvc.RollbackAsync("Threshold", app, null, null, 0, "u", CancellationToken.None);

        // Legacy restored to 100
        var legacyRow = (await settings.QueryAsync(new SettingQuery { ApplicationId = app, KeyPrefix = "Threshold" }, CancellationToken.None))
            .Single(r => r.ClientAppVersion == null);
        legacyRow.Value.Should().Be("100");

        // v2.0.0 unchanged at 200
        var newRow = (await settings.QueryAsync(new SettingQuery { ApplicationId = app, ClientAppVersion = "v2.0.0", KeyPrefix = "Threshold" }, CancellationToken.None))
            .Single();
        newRow.Value.Should().Be("200");
    }
}
