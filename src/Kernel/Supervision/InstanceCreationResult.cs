namespace Kernel.Supervision;

public enum InstanceCreationOutcome
{
    Created,
    RejectedGovernance,
    RejectedInsufficientBudget
}

public sealed record InstanceCreationResult(InstanceCreationOutcome Outcome, InstanceHandle? Handle, string? Reason)
{
    public static InstanceCreationResult Created(InstanceHandle handle) => new(InstanceCreationOutcome.Created, handle, null);

    public static InstanceCreationResult Rejected(InstanceCreationOutcome outcome, string reason) => new(outcome, null, reason);
}
