namespace Runtime.Hosts.Strategy;

public enum StrategyActivationOutcome
{
    Created,
    Rejected,
}

public sealed record StrategyActivationResult(StrategyActivationOutcome Outcome, StrategyInstanceHandle? Handle, string? Reason)
{
    public static StrategyActivationResult Created(StrategyInstanceHandle handle) => new(StrategyActivationOutcome.Created, handle, Reason: null);

    public static StrategyActivationResult Rejected(string reason) => new(StrategyActivationOutcome.Rejected, Handle: null, reason);
}
