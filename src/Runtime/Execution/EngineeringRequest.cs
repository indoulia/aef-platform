namespace Runtime.Execution;

// The external entry-point payload. Payload is a deliberately open,
// on-demand-fetched bag (mirroring RuntimeExecutionContext.Tier3SkillContent's
// own "fetched on demand" design) rather than a rigid closed schema, since
// different routed capabilities (Delivery Execution vs. a Workflow trigger)
// need different structured input.
public sealed record EngineeringRequest(
    string UserRequest,
    string ProjectProfileId,
    IReadOnlyDictionary<string, object>? Payload = null);
