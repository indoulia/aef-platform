namespace Runtime.Agent.Health;

public sealed record RuntimeHealthReport(string Name, RuntimeHealthStatus Status, string? Reason);
