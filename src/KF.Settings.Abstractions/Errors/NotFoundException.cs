namespace KF.Settings.Errors;

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string key, string? app, string? inst)
        : base(ErrorCode.NotFound, $"Key not found '{key}'.", key, app, inst) { }
}
