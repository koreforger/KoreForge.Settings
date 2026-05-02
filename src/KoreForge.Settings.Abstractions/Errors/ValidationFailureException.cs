namespace KoreForge.Settings.Errors;

public sealed class ValidationFailureException : DomainException
{
    public IReadOnlyList<string> Errors { get; }

    public ValidationFailureException(string contextName, IEnumerable<string> errors)
        : base(ErrorCode.ValidationFailure, $"Validation failed for '{contextName}'.")
    {
        Errors = errors.ToList();
    }
}
