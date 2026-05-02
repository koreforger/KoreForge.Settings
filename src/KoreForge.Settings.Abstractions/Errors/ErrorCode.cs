namespace KoreForge.Settings.Errors;

public enum ErrorCode
{
    None = 0,
    ConcurrencyConflict = 1,
    MissingRowVersion = 2,
    RollbackConflict = 3,
    ValidationFailure = 4,
    DecryptionFailure = 5,
    NotFound = 6,
    DuplicateKey = 7,
    InvalidArgument = 8
}
