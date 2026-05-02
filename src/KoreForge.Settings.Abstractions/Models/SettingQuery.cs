namespace KoreForge.Settings.Models;

public sealed record SettingQuery
{
    public string? ApplicationId { get; init; }
    public string? InstanceId { get; init; }
    public string? ClientAppVersion { get; init; }
    public string? KeyPrefix { get; init; }
    public bool? IsSecret { get; init; }
    public int? Skip { get; init; }
    public int? Take { get; init; }
}
