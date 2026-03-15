using KF.Settings.Extensions;
using KF.Settings.Interfaces;
using KF.Settings.Models;
using KF.Settings.Options;
using KF.Settings.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Register KF Settings with live SQL Server reload
builder.Configuration.AddKFSettings(opts =>
{
    opts.ApplicationId = "kf-settings-sample";
    opts.EnableMetrics = true;
    opts.PollingInterval = TimeSpan.FromSeconds(30);
});

builder.Services.AddKFSettingsServices(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// ------- Settings CRUD endpoints -------

app.MapGet("/settings", async (ISettingsService svc, KFSettingsOptions opts, CancellationToken ct) =>
{
    var rows = await svc.QueryAsync(new SettingQuery { ApplicationId = opts.ApplicationId }, ct);
    return rows.Select(r => new { r.Id, r.Key, Value = r.IsSecret ? "***" : r.Value, Binary = r.BinaryValue != null, r.IsSecret, r.ModifiedDateUtc });
});

app.MapGet("/settings/{id:long}", async (long id, ISettingsService svc, CancellationToken ct) =>
{
    var row = await svc.GetAsync(id, ct);
    return row is null ? Results.NotFound() : Results.Ok(row);
});

app.MapPost("/settings", async (SettingUpsert request, ISettingsService svc, CancellationToken ct) =>
{
    var row = await svc.UpsertAsync(request with { ChangedBy = "sample-api" }, ct);
    return Results.Created($"/settings/{row.Id}", row);
});

app.MapPut("/settings/{id:long}", async (long id, SettingUpsert request, ISettingsService svc, CancellationToken ct) =>
{
    var row = await svc.UpsertAsync(request with { Id = id, ChangedBy = "sample-api" }, ct);
    return Results.Ok(row);
});

app.MapDelete("/settings/{id:long}", async (long id, string rowVersion, ISettingsService svc, CancellationToken ct) =>
{
    await svc.DeleteAsync(id, "sample-api", Convert.FromHexString(rowVersion), ct);
    return Results.NoContent();
});

app.MapGet("/settings/{key}/history", async (string key, IHistoryService svc, ISettingsService settingsSvc, KFSettingsOptions opts, CancellationToken ct) =>
{
    var rows = await settingsSvc.QueryAsync(new SettingQuery { ApplicationId = opts.ApplicationId, KeyPrefix = key }, ct);
    var match = rows.FirstOrDefault(r => r.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    if (match is null) return Results.NotFound();
    var history = await svc.GetHistoryAsync(match.Id, ct);
    return Results.Ok(history);
});

app.MapGet("/health/settings", (IHealthReporter health) =>
    Results.Ok(new { health.LastSuccessfulReloadUtc, health.ConsecutiveFailures, health.LastRowCount, health.LastHashSnippet }));

app.Run();
