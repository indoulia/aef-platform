namespace Runtime.Agent.Capabilities;

public sealed record CapabilityDescriptor(
    string CapabilityId,
    string DisplayName,
    string Description,
    IReadOnlyList<string> RequiredPayloadKeys,
    string ExecutionMode);
