namespace KF.Settings.Errors;

public sealed class InvalidArgumentException : DomainException
{
    public InvalidArgumentException(string message, string? key = null, string? app = null, string? inst = null)
        : base(ErrorCode.InvalidArgument, message, key, app, inst) { }
}
