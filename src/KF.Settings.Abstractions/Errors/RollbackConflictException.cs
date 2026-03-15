namespace KF.Settings.Errors;

public sealed class RollbackConflictException : DomainException
{
    public RollbackConflictException(string key, string? app, string? inst, byte[]? expected, byte[]? actual)
        : base(ErrorCode.RollbackConflict, $"Rollback conflict for key '{key}'.", key, app, inst, expected, actual) { }

    public RollbackConflictException(ErrorCode code, string message, string? keyPath = null, string? appId = null, string? instanceId = null, byte[]? expectedRv = null, byte[]? actualRv = null, Exception? inner = null)
        : base(code, message, keyPath, appId, instanceId, expectedRv, actualRv, inner) { }
}
