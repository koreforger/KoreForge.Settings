namespace KF.Settings.Errors;

public sealed class DecryptionFailureException : DomainException
{
    public DecryptionFailureException(string key, string? app, string? inst, Exception inner)
        : base(ErrorCode.DecryptionFailure, $"Decryption failed for key '{key}'.", key, app, inst, inner: inner) { }
}
