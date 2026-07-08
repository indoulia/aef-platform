namespace Runtime.DomainContent.ConversationManagement;

// Mirrors PCT-EXE-0021's Input/Output shape.
public enum RoutingOutcome
{
    Routed,
    NeedsClarification,
    ContextDiscoveryRequired
}

public sealed record RoutingRequest(string UserRequest, ConversationContext ConversationContext);

public sealed record RoutingResult(
    RoutingOutcome Outcome,
    string? TargetCapabilityId,
    IReadOnlyList<string>? ClarificationQuestions,
    string? DiscoveryTarget)
{
    public static RoutingResult Routed(string targetCapabilityId) =>
        new(RoutingOutcome.Routed, targetCapabilityId, null, null);

    public static RoutingResult NeedsClarification(IReadOnlyList<string> questions) =>
        new(RoutingOutcome.NeedsClarification, null, questions, null);

    public static RoutingResult ContextDiscoveryRequired(string discoveryTarget) =>
        new(RoutingOutcome.ContextDiscoveryRequired, null, null, discoveryTarget);
}
