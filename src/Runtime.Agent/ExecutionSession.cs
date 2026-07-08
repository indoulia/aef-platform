namespace Runtime.Agent;

// Pure caller convenience, process-lifetime only (InMemorySessionStore,
// Task 5). Never influences routing or dispatch - those happen exclusively
// inside EngineeringExecutionPipeline, per request, deterministically.
public sealed record ExecutionSession(
    string SessionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    string? LastProjectProfileId,
    IReadOnlyDictionary<string, object>? LastPayload,
    int TurnCount);
