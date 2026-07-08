namespace Runtime.Agent.Security;

public sealed record SecurityContext(string? PrincipalId, IReadOnlyDictionary<string, string>? Claims);
