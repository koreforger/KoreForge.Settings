using KoreForge.Settings.Errors;
using KoreForge.Settings.Options;
using Microsoft.Extensions.Options;

namespace KoreForge.Settings.Validation;

internal sealed class OptionsValidator : IValidateOptions<KoreForgeSettingsOptions>
{
    public ValidateOptionsResult Validate(string? name, KoreForgeSettingsOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ApplicationId)) errors.Add("ApplicationId is required");
        if (options.PollingInterval < TimeSpan.FromSeconds(30)) errors.Add("PollingInterval must be >= 30s");
        if (errors.Count == 0) return ValidateOptionsResult.Success;
        if (options.FailOnValidationErrors)
            throw new ValidationFailureException(nameof(KoreForgeSettingsOptions), errors);
        return ValidateOptionsResult.Fail(errors);
    }
}
