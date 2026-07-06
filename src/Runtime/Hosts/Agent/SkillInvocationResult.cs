namespace Runtime.Hosts.Agent;

public enum SkillInvocationOutcome
{
    Invoked,
    CapabilityGap,
}

public sealed record SkillInvocationResult(SkillInvocationOutcome Outcome, object? Output, string? Reason)
{
    public static SkillInvocationResult CapabilityGap(string reason) =>
        new(SkillInvocationOutcome.CapabilityGap, Output: null, reason);
}
