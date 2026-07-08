namespace Runtime.Integration.RepositoryOperations;

public enum RepositoryOperationStatus
{
    Success,
    RootNotReadable,
    PathEscapesRoot,
    PathNotFound,
    OperationFailed,
}

// The uniform structured-result shape for every mutating Repository
// Operation - never an unhandled exception, never a silent no-op.
public sealed record RepositoryOperationResult(RepositoryOperationStatus Status, string? FailureReason)
{
    public static readonly RepositoryOperationResult Ok = new(RepositoryOperationStatus.Success, null);

    public static RepositoryOperationResult Failure(RepositoryOperationStatus status, string reason) => new(status, reason);
}
