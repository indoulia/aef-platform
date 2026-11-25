namespace Runtime.Integration.GitOperations;

// Files changed relative to a base: committed since it, staged, unstaged, and untracked. Forward slashes, repository-relative.
public sealed record GitChangedFilesResult(bool Succeeded, string? FailureReason, string? ResolvedBaseRef, IReadOnlyList<string> Files)
{
    public static GitChangedFilesResult Failed(string reason) => new(false, reason, null, Array.Empty<string>());
}
