namespace Runtime.Agent;

public sealed record ExecutionResponse(
    string RequestId,
    string? SessionId,
    ExecutionStatus Status,
    ExecutionError? Error,
    string? PresentedResult,
    string? SuggestedNextStep,
    ExecutionEvidence Evidence,
    ExecutionMetadata Metadata);
