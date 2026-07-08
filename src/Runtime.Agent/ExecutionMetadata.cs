namespace Runtime.Agent;

public sealed record ExecutionMetadata(
    string? ExecutedCapabilityId,
    string? ExecutedAgentId,
    TimeSpan Duration,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
