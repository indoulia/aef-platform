namespace Runtime.Integration.RepositoryOperations;

public enum RepositoryFileReadStatus
{
    Success,
    RootNotReadable,
    PathEscapesRoot,
    FileNotFound,
    ReadFailed,
}

// Either the real file content, or a structured failure reason - never an
// unhandled exception (mirrors RepositoryScanResult's own shape).
public sealed record RepositoryFileReadResult(RepositoryFileReadStatus Status, string? Content, string? FailureReason)
{
    public static RepositoryFileReadResult Success(string content) => new(RepositoryFileReadStatus.Success, content, null);

    public static RepositoryFileReadResult Failure(RepositoryFileReadStatus status, string reason) => new(status, null, reason);
}
