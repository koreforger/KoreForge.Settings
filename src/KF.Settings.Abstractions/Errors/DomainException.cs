namespace KF.Settings.Errors;

public abstract class DomainException : Exception
{
    public ErrorCode Code { get; }
    public string? KeyPath { get; }
    public string? ApplicationId { get; }
    public string? InstanceId { get; }
    public string? ExpectedRowVersionHex { get; }
    public string? ActualRowVersionHex { get; }

    protected DomainException(ErrorCode code, string message, string? keyPath = null, string? appId = null, string? instanceId = null, byte[]? expectedRv = null, byte[]? actualRv = null, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
        KeyPath = keyPath;
        ApplicationId = appId;
        InstanceId = instanceId;
        ExpectedRowVersionHex = expectedRv is null ? null : Convert.ToHexString(expectedRv);
        ActualRowVersionHex = actualRv is null ? null : Convert.ToHexString(actualRv);
    }
}
