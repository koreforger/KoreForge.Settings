using KF.Settings.Errors;
using KF.Settings.Options;
using Microsoft.Extensions.Options;

namespace KF.Settings.Validation;

internal sealed class OptionsValidator : IValidateOptions<KFSettingsOptions>
{
    public ValidateOptionsResult Validate(string? name, KFSettingsOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ApplicationId)) errors.Add("ApplicationId is required");
        if (options.PollingInterval < TimeSpan.FromSeconds(30)) errors.Add("PollingInterval must be >= 30s");
        if (errors.Count == 0) return ValidateOptionsResult.Success;
        if (options.FailOnValidationErrors)
            throw new ValidationFailureException(nameof(KFSettingsOptions), errors);
        return ValidateOptionsResult.Fail(errors);
    }
}
