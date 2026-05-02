using KoreForge.Settings.Models;

namespace KoreForge.Settings.Options;

public sealed class KoreForgeSettingsOptions
{
    public string? ConnectionString { get; set; }
    public string? ApplicationId { get; set; }
    public string? InstanceId { get; set; }
    public string? ClientAppVersion { get; set; }
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(1);
    public BinaryEncoding BinaryEncoding { get; set; } = BinaryEncoding.Base64Url;
    public bool FailFastOnStartup { get; set; } = true;

    // Validation
    public bool EnableDataAnnotations { get; set; } = true;
    public bool EnableFluentValidation { get; set; } = true;
    public bool FailOnValidationErrors { get; set; } = true;

    // Security
    public bool EnableDecryption { get; set; } = false;
    public bool RequireDeterministicEncryption { get; set; } = false;

    // Diagnostics
    public bool EnableMetrics { get; set; } = true;
    public bool EnableDetailedLogging { get; set; } = false;

    // Concurrency
    public bool ThrowOnConcurrencyViolation { get; set; } = true;
}
