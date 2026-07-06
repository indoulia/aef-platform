namespace Runtime.Integration.RepositoryScanning;

public enum RepositoryScanStatus
{
    Success,
    RootNotReadable,
    PathEscapesRoot,
    PathNotFound,
}

// Either a successful, read-only directory listing, or a structured
// failure reason - never an unhandled exception, never a silent
// truncation (Epic 0008, Story S22 acceptance criterion 2).
public sealed record RepositoryScanResult(RepositoryScanStatus Status, IReadOnlyList<RepositoryEntry> Entries, string? FailureReason)
{
    public static RepositoryScanResult Success(IReadOnlyList<RepositoryEntry> entries) =>
        new(RepositoryScanStatus.Success, entries, FailureReason: null);

    public static RepositoryScanResult Failure(RepositoryScanStatus status, string reason) =>
        new(status, Array.Empty<RepositoryEntry>(), reason);
}
