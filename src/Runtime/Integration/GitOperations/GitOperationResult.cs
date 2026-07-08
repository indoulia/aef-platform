namespace Runtime.Integration.GitOperations;

public sealed record GitOperationResult(bool Success, string? FailureReason)
{
    public static readonly GitOperationResult Ok = new(true, null);

    public static GitOperationResult Failure(string reason) => new(false, reason);
}
