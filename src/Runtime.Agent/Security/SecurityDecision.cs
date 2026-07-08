namespace Runtime.Agent.Security;

public sealed record SecurityDecision(bool IsAllowed, string? DenialReason);
