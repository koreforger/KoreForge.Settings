# KoreForge.Settings

SQL Server-backed live-reload configuration provider with multi-app scoping, encryption, history tracking, and rollback support.

## Packages

| Assembly | Description |
|----------|-------------|
| `KF.Settings` | Configuration provider, hot-reload background service |
| `KF.Settings.Abstractions` | Models, interfaces, options |
| `KF.Settings.Core` | `SettingsService`, `HistoryService`, binary accessor |
| `KF.Settings.Data` | EF Core `KFSettingsDbContext` |
| `KF.Settings.Encryption` | `IEncryptionProvider` contract + `NoOpEncryptionProvider` |
| `KF.Settings.Metrics` | In-memory metrics recorder for reload operations |
| `KF.Settings.Cli` | CLI tool: `list`, `get`, `set`, `delete`, `history`, `rollback`, `export`, `import` |

## Installation

```xml
<PackageReference Include="KoreForge.Settings" Version="0.0.6-alpha" />
```

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Add KF Settings as a configuration source
builder.Configuration.AddKFSettings(opts =>
{
    opts.ApplicationId = "my-app";
    opts.PollingInterval = TimeSpan.FromSeconds(30);
    opts.EnableMetrics = true;
});

// 2. Register services (resolves connection string from config/env)
builder.Services.AddKFSettingsServices(builder.Configuration);

var app = builder.Build();

// 3. Use ISettingsService for CRUD
app.MapGet("/settings", async (ISettingsService svc, KFSettingsOptions opts, CancellationToken ct) =>
{
    var rows = await svc.QueryAsync(new SettingQuery { ApplicationId = opts.ApplicationId }, ct);
    return rows;
});

app.MapPost("/settings", async (SettingUpsert request, ISettingsService svc, CancellationToken ct) =>
{
    var row = await svc.UpsertAsync(request with { ChangedBy = "api" }, ct);
    return Results.Created($"/settings/{row.Id}", row);
});
```

## Connection String Resolution

The connection string is resolved in order:

1. `KFSettingsOptions.ConnectionString` (set in `AddKFSettings` callback)
2. `ConnectionStrings:KFSettings` in `appsettings.json`
3. `KF:Settings:ConnectionString` in configuration
4. `KF_SETTINGS_CONNECTIONSTRING` environment variable

## Key Interfaces

```csharp
public interface ISettingsService
{
    Task<IReadOnlyList<SettingRow>> QueryAsync(SettingQuery filter, CancellationToken ct);
    Task<SettingRow?> GetAsync(long id, CancellationToken ct);
    Task<SettingRow> UpsertAsync(SettingUpsert request, CancellationToken ct);
    Task DeleteAsync(long id, string changedBy, byte[] expectedRowVersion, CancellationToken ct);
}

public interface IHistoryService
{
    Task<IReadOnlyList<SettingsHistoryRow>> GetHistoryAsync(long settingId, CancellationToken ct);
    Task RollbackAsync(string key, int versionIndex, string changedBy, CancellationToken ct);
}
```

## Configuration Options

| Property | Default | Description |
|----------|---------|-------------|
| `ConnectionString` | _(resolved)_ | SQL Server connection |
| `ApplicationId` | `null` | Scope filter for multi-app isolation |
| `InstanceId` | `null` | Optional instance-level scope |
| `ClientAppVersion` | `null` | Optional client binary version for staged rollout isolation (e.g. `"v2.0.0"`) |
| `PollingInterval` | `60s` | Background reload frequency (min 30s) |
| `BinaryEncoding` | `Base64Url` | Encoding for binary settings |
| `FailFastOnStartup` | `true` | Throw on startup validation failure |
| `EnableDecryption` | `false` | Enable value decryption (requires `IEncryptionProvider`) |
| `EnableMetrics` | `true` | In-memory metrics collection |
| `EnableDetailedLogging` | `false` | Verbose reload traces |

## Hot Reload

The `SettingsReloadBackgroundService` automatically polls SQL Server at the configured interval. It detects changes via row count + max row version + key checksum, and atomically rebuilds the configuration snapshot. Health is exposed via `IHealthReporter`:

## ClientAppVersion — Staged Rollout Isolation

When deploying a new binary version alongside the old one, set `ClientAppVersion` in options so the new binary reads version-specific setting overrides without affecting the old binary:

```csharp
services.AddKFSettings(opts =>
{
    opts.ApplicationId = "my-app";
    opts.ClientAppVersion = "v2.0.0"; // new binary only; omit or null for legacy binary
});
```

Resolution precedence (first match per key wins, highest priority first):

| Level | Scope |
|-------|-------|
| 1 | `(app, instance, clientVersion)` |
| 2 | `(app, null, clientVersion)` |
| 3 | `(app, instance, null)` |
| 4 | `(app, null, null)` |
| 5 | `(null, null, null)` — global |

Rows with `ClientAppVersion IS NULL` continue to work exactly as before (backward compatible). After the rollout window, clean up version-specific overrides using the CLI:

```bash
kf-settings list --application my-app --client-version v2.0.0
kf-settings export --application my-app --client-version v2.0.0 --file rollout-overrides.json
```

```csharp
app.MapGet("/health/settings", (IHealthReporter health) =>
    new { health.LastSuccessfulReloadUtc, health.ConsecutiveFailures, health.LastRowCount });
```

## CLI Tool

```bash
dotnet tool install -g KoreForge.Settings.Cli

kf-settings list --application my-app --connection "Server=...;Database=...;"
kf-settings set --application my-app --key "Feature:Enabled" --value "true"
kf-settings history --application my-app --key "Feature:Enabled"
kf-settings rollback --application my-app --key "Feature:Enabled" --version 2
kf-settings export --application my-app > settings.json
kf-settings import --application my-app < settings.json
```

## License

[MIT](LICENSE.md)
