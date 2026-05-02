using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using KoreForge.Settings.Interfaces;
using KoreForge.Settings.Models;
using Microsoft.Extensions.DependencyInjection;

namespace KoreForge.Settings.Cli.Commands;

internal static class ExportImport
{
    public static Command CreateExport(Func<InvocationContext, RootServices> servicesFactory)
    {
        var fileOpt = new Option<string>("--file", "Output file") { IsRequired = true };
        var includeSecretsOpt = new Option<bool>("--include-secrets", "Include secret values");
        var cmd = new Command("export", "Export settings to JSON") { fileOpt, includeSecretsOpt };
        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var file = ctx.ParseResult.GetValueForOption(fileOpt)!;
            var includeSecrets = ctx.ParseResult.GetValueForOption(includeSecretsOpt);
            var rs = servicesFactory(ctx);
            var svc = rs.Provider.GetRequiredService<ISettingsService>();
            var rows = await svc.QueryAsync(new SettingQuery { ApplicationId = rs.Options.ApplicationId, InstanceId = rs.Options.InstanceId, ClientAppVersion = rs.Options.ClientAppVersion }, ctx.GetCancellationToken());
            var shaped = rows.Select(r => new { r.ApplicationId, r.InstanceId, r.ClientAppVersion, r.Key, Value = (r.IsSecret && !includeSecrets) ? null : r.Value, r.IsSecret, r.ValueEncrypted, r.Comment });
            var json = JsonSerializer.Serialize(shaped, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(file, json, ctx.GetCancellationToken());
            Console.WriteLine($"Exported {rows.Count} settings -> {file}");
        });
        return cmd;
    }

    public static Command CreateImport(Func<InvocationContext, RootServices> servicesFactory)
    {
        var fileOpt = new Option<string>("--file", "Input JSON file") { IsRequired = true };
        var applyOpt = new Option<bool>("--apply", () => false, "Apply changes (otherwise dry-run)");
        var upsertOpt = new Option<bool>("--upsert", () => true, "Upsert entries (false = insert only)");
        var assumeSecretOpt = new Option<bool>("--assume-secret", () => false, "Treat all imported values as secret");
        var cmd = new Command("import", "Import settings from JSON") { fileOpt, applyOpt, upsertOpt, assumeSecretOpt };
        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var file = ctx.ParseResult.GetValueForOption(fileOpt)!;
            var apply = ctx.ParseResult.GetValueForOption(applyOpt);
            var upsert = ctx.ParseResult.GetValueForOption(upsertOpt);
            var assumeSecret = ctx.ParseResult.GetValueForOption(assumeSecretOpt);
            var rs = servicesFactory(ctx);
            if (!File.Exists(file)) { Console.Error.WriteLine("File not found"); ctx.ExitCode = 2; return; }
            var text = await File.ReadAllTextAsync(file, ctx.GetCancellationToken());
            var docs = JsonSerializer.Deserialize<List<ImportDoc>>(text) ?? new();
            Console.WriteLine($"Read {docs.Count} entries");
            if (!apply) { Console.WriteLine("Dry run complete (use --apply to persist)"); return; }
            var svc = rs.Provider.GetRequiredService<ISettingsService>();
            foreach (var d in docs)
            {
                try
                {
                    var upsertRequest = new SettingUpsert
                    {
                        ApplicationId = d.ApplicationId ?? rs.Options.ApplicationId,
                        InstanceId = d.InstanceId ?? rs.Options.InstanceId,
                        ClientAppVersion = d.ClientAppVersion ?? rs.Options.ClientAppVersion,
                        Key = d.Key,
                        Value = d.Value,
                        IsSecret = assumeSecret || d.IsSecret,
                        ChangedBy = Environment.UserName
                    };
                    if (!upsert)
                    {
                        var existing = await svc.QueryAsync(new SettingQuery { ApplicationId = upsertRequest.ApplicationId, InstanceId = upsertRequest.InstanceId, ClientAppVersion = upsertRequest.ClientAppVersion, KeyPrefix = d.Key }, ctx.GetCancellationToken());
                        if (existing.Any(e => e.Key.Equals(d.Key, StringComparison.OrdinalIgnoreCase)))
                        {
                            Console.WriteLine($"Skip existing {d.Key}");
                            continue;
                        }
                    }
                    await svc.UpsertAsync(upsertRequest, ctx.GetCancellationToken());
                }
                catch (Exception ex)
                { Console.Error.WriteLine($"Failed {d.Key}: {ex.Message}"); }
            }
            Console.WriteLine("Import complete");
        });
        return cmd;
    }

    internal sealed record ImportDoc(string Key, string? Value, bool IsSecret, string? ApplicationId, string? InstanceId, string? ClientAppVersion);
}
