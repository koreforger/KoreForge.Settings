namespace KoreForge.Settings.Errors;

public sealed class MissingRowVersionException : DomainException
{
    public MissingRowVersionException(string key, string? app, string? inst)
        : base(ErrorCode.MissingRowVersion, $"Missing expected rowversion for key '{key}'.", key, app, inst) { }
}
