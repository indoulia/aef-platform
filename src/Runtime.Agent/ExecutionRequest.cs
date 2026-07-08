namespace Runtime.Agent;

// The public, channel-agnostic execution request every future GA-3 channel
// (CLI/REST/IDE/Chat) will construct. ProjectProfileId is a required
// string, matching Runtime.Execution.EngineeringRequest's own shape; an
// empty string on a request carrying a SessionId means "inherit the
// session's last value" (AgentExecutionService, Task 6) - there is no
// separate nullable variant of this field to keep this contract a direct,
// honest mirror of EngineeringRequest.
public sealed record ExecutionRequest(
    string RequestId,
    string? SessionId,
    string UserRequest,
    string ProjectProfileId,
    IReadOnlyDictionary<string, object>? Payload,
    IExecutionProgressObserver? ProgressObserver = null);
