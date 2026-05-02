namespace KoreForge.Settings.Errors;

public sealed class DuplicateKeyException : DomainException
{
    public DuplicateKeyException(string key, string? app, string? inst)
        : base(ErrorCode.DuplicateKey, $"Duplicate key for scope '{app ?? "<global>"}:{inst ?? "<app>"}' path '{key}'.", key, app, inst) { }
}
