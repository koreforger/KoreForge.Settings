namespace KF.Settings.Errors;

public sealed class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException(string key, string? app, string? inst, byte[]? expected, byte[]? actual)
        : base(ErrorCode.ConcurrencyConflict, $"Concurrency conflict for key '{key}'.", key, app, inst, expected, actual) { }
}
