namespace Kernel.Governance;

public enum ActivationOutcome
{
    Authorized,
    RejectedNotRegistered,
    RejectedNotActive,
    RejectedNoPolicyResolver,
    RejectedPolicyDenied
}

public sealed record ActivationResult(ActivationOutcome Outcome, string? Reason)
{
    public static ActivationResult Authorized() => new(ActivationOutcome.Authorized, null);

    public static ActivationResult Rejected(ActivationOutcome outcome, string reason) => new(outcome, reason);
}
