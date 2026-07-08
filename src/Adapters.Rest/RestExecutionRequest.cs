namespace Adapters.Rest;

public sealed record RestExecutionRequest(string? SessionId, string UserRequest, string ProjectProfileId, IReadOnlyDictionary<string, object>? Payload);
