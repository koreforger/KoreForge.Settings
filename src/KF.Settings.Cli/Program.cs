using System.CommandLine;
using System.CommandLine.Invocation;
using KF.Settings.Core.Services;
using KF.Settings.Data;
using KF.Settings.Encryption;
using KF.Settings.Errors;
using KF.Settings.Interfaces;
using KF.Settings.Metrics;
using KF.Settings.Models;
using KF.Settings.Options;
using KF.Settings.Cli.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var root = new RootCommand("KoreForge Settings CLI — manage SQL Server-backed multi-app settings");

var appOpt = new Option<string>("--application", description: "Application Id") { IsRequired = true };
var instOpt = new Option<string?>("--instance", () => null, "Instance Id");
var connOpt = new Option<string?>("--connection", () => null, "SQL Server connection string (overrides KF_SETTINGS_CONNECTIONSTRING env var)");
root.AddGlobalOption(appOpt);
root.AddGlobalOption(instOpt);
root.AddGlobalOption(connOpt);

RootServices BuildServices(InvocationContext ctx) => BuildServicesInternal(ctx);

RootServices BuildServicesInternal(InvocationContext ctx)
{
    var app = ctx.ParseResult.GetValueForOption(appOpt)!;
    var inst = ctx.ParseResult.GetValueForOption(instOpt);
    var conn = ctx.ParseResult.GetValueForOption(connOpt)
               ?? Environment.GetEnvironmentVariable("KF_SETTINGS_CONNECTIONSTRING")
               ?? throw new InvalidOperationException("Connection string required. Use --connection or set KF_SETTINGS_CONNECTIONSTRING.");
    var opts = new KFSettingsOptions { ApplicationId = app, InstanceId = inst, ConnectionString = conn };
    var services = new ServiceCollection();
    services.AddSingleton(opts);
    services.AddSingleton<IEncryptionProvider, NoOpEncryptionProvider>();
    services.AddSingleton<IMetricsRecorder, NoOpMetricsRecorder>();
    services.AddDbContextFactory<KFSettingsDbContext>(o => o.UseSqlServer(opts.ConnectionString));
    services.AddScoped<ISettingsService, SettingsService>();
    services.AddScoped<IHistoryService, HistoryService>();
    var sp = services.BuildServiceProvider();
    return new RootServices(sp, opts);
}

root.SetHandler((InvocationContext ctx) =>
{
    Console.WriteLine("Commands: list, get, set, delete, history, rollback, export, import");
});

// list
var list = new Command("list", "List settings");
var secretsOpt = new Option<bool>("--secrets", "Include secrets");
list.AddOption(secretsOpt);
list.SetHandler(async (InvocationContext ctx) =>
{
    var includeSecrets = ctx.ParseResult.GetValueForOption(secretsOpt);
    var rs = BuildServices(ctx);
    var svc = rs.Provider.GetRequiredService<ISettingsService>();
    var query = new SettingQuery { ApplicationId = rs.Options.ApplicationId, InstanceId = rs.Options.InstanceId };
    var rows = await svc.QueryAsync(query, ctx.GetCancellationToken());
    foreach (var r in rows)
    {
        if (!r.IsSecret || includeSecrets)
            Console.WriteLine($"{r.Id}\t{r.Key}\t{(r.Value is not null ? Trunc(r.Value) : "<binary>")}");
        else
            Console.WriteLine($"{r.Id}\t{r.Key}\t*** (secret masked)");
    }
});
root.Add(list);

// get
var get = new Command("get", "Get a setting by id");
var getIdArg = new Argument<long>("id");
get.AddArgument(getIdArg);
get.SetHandler(async (InvocationContext ctx) =>
{
    var id = ctx.ParseResult.GetValueForArgument(getIdArg);
    var rs = BuildServices(ctx);
    var svc = rs.Provider.GetRequiredService<ISettingsService>();
    var row = await svc.GetAsync(id, ctx.GetCancellationToken());
    if (row == null) { Console.Error.WriteLine("Not found"); ctx.ExitCode = 1; return; }
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(row));
});
root.Add(get);

// set (upsert)
var set = new Command("set", "Insert or update a setting");
var keyArg = new Argument<string>("key");
var valueOpt = new Option<string?>("--value", () => null, "Text value (mutually exclusive with --file)");
var fileOpt = new Option<string?>("--file", () => null, "Path to file to store as binary");
var secretOpt = new Option<bool>("--secret", "Mark as secret");
var rowVersionOpt = new Option<string?>("--rowversion", () => null, "Expected rowversion hex for update");
set.AddArgument(keyArg);
set.AddOption(valueOpt);
set.AddOption(fileOpt);
set.AddOption(secretOpt);
set.AddOption(rowVersionOpt);
set.SetHandler(async (InvocationContext ctx) =>
{
    var key = ctx.ParseResult.GetValueForArgument(keyArg);
    var val = ctx.ParseResult.GetValueForOption(valueOpt);
    var file = ctx.ParseResult.GetValueForOption(fileOpt);
    var secret = ctx.ParseResult.GetValueForOption(secretOpt);
    var rvHex = ctx.ParseResult.GetValueForOption(rowVersionOpt);
    if ((val is null) == (file is null)) { Console.Error.WriteLine("Specify exactly one of --value or --file"); ctx.ExitCode = 2; return; }
    byte[]? bin = null;
    if (file != null) bin = await File.ReadAllBytesAsync(file, ctx.GetCancellationToken());
    byte[]? expected = rvHex != null ? Convert.FromHexString(rvHex) : null;
    var rs = BuildServices(ctx);
    var svc = rs.Provider.GetRequiredService<ISettingsService>();
    try
    {
        var result = await svc.UpsertAsync(new SettingUpsert
        {
            Key = key, Value = val, BinaryValue = bin, IsSecret = secret,
            ChangedBy = Environment.UserName, ExpectedRowVersion = expected,
            ApplicationId = rs.Options.ApplicationId, InstanceId = rs.Options.InstanceId
        }, ctx.GetCancellationToken());
        Console.WriteLine($"OK Id={result.Id} RowVersion={Convert.ToHexString(result.RowVersion)}");
    }
    catch (DomainException ex) { Console.Error.WriteLine($"ERROR {ex.Code}: {ex.Message}"); ctx.ExitCode = 1; }
});
root.Add(set);

// delete
var del = new Command("delete", "Delete a setting by id");
var delIdArg = new Argument<long>("id");
var delRvOpt = new Option<string>("--rowversion", description: "Expected rowversion hex") { IsRequired = true };
del.AddArgument(delIdArg);
del.AddOption(delRvOpt);
del.SetHandler(async (InvocationContext ctx) =>
{
    var id = ctx.ParseResult.GetValueForArgument(delIdArg);
    var rvHex = ctx.ParseResult.GetValueForOption(delRvOpt);
    if (rvHex is null) { Console.Error.WriteLine("--rowversion required"); ctx.ExitCode = 2; return; }
    var expected = Convert.FromHexString(rvHex);
    var rs = BuildServices(ctx);
    var svc = rs.Provider.GetRequiredService<ISettingsService>();
    try { await svc.DeleteAsync(id, Environment.UserName, expected, ctx.GetCancellationToken()); Console.WriteLine("Deleted"); }
    catch (DomainException ex) { Console.Error.WriteLine($"ERROR {ex.Code}: {ex.Message}"); ctx.ExitCode = 1; }
});
root.Add(del);

// history
var hist = new Command("history", "List history entries for a setting id");
var histSettingIdArg = new Argument<long>("settingId");
hist.AddArgument(histSettingIdArg);
hist.SetHandler(async (InvocationContext ctx) =>
{
    var sid = ctx.ParseResult.GetValueForArgument(histSettingIdArg);
    var rs = BuildServices(ctx);
    var hs = rs.Provider.GetRequiredService<IHistoryService>();
    var entries = await hs.GetHistoryAsync(sid, ctx.GetCancellationToken());
    foreach (var h in entries)
        Console.WriteLine($"{h.HistoryId}\t{h.Operation}\t{h.ChangedDateUtc:o}\tOldRv={Hex(h.RowVersionBefore)} NewRv={Hex(h.RowVersionAfter)}");
});
root.Add(hist);

// rollback
var rollback = new Command("rollback", "Rollback a key to a previous version (0=latest history entry)");
var rollbackKeyArg = new Argument<string>("key");
var rollbackIndexArg = new Argument<int>("index");
rollback.AddArgument(rollbackKeyArg);
rollback.AddArgument(rollbackIndexArg);
rollback.SetHandler(async (InvocationContext ctx) =>
{
    var key = ctx.ParseResult.GetValueForArgument(rollbackKeyArg);
    var idx = ctx.ParseResult.GetValueForArgument(rollbackIndexArg);
    var rs = BuildServices(ctx);
    var hs = rs.Provider.GetRequiredService<IHistoryService>();
    try { await hs.RollbackAsync(key, idx, Environment.UserName, ctx.GetCancellationToken()); Console.WriteLine("Rolled back"); }
    catch (DomainException ex) { Console.Error.WriteLine($"ERROR {ex.Code}: {ex.Message}"); ctx.ExitCode = 1; }
});
root.Add(rollback);

root.Add(ExportImport.CreateExport(BuildServicesInternal));
root.Add(ExportImport.CreateImport(BuildServicesInternal));

return await root.InvokeAsync(args);

static string Trunc(string v) => v.Length <= 80 ? v : v[..77] + "...";
static string Hex(byte[]? bytes) => bytes == null ? "-" : Convert.ToHexString(bytes);

readonly record struct RootServices(IServiceProvider Provider, KFSettingsOptions Options);
