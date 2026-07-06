namespace Runtime.Hosts.Agent;

public enum AgentActivationOutcome
{
    Created,
    Rejected,
}

public sealed record AgentActivationResult(AgentActivationOutcome Outcome, AgentInstanceHandle? Handle, string? Reason)
{
    public static AgentActivationResult Created(AgentInstanceHandle handle) => new(AgentActivationOutcome.Created, handle, Reason: null);

    public static AgentActivationResult Rejected(string reason) => new(AgentActivationOutcome.Rejected, Handle: null, reason);
}
