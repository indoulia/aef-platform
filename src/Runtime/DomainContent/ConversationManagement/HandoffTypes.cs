namespace Runtime.DomainContent.ConversationManagement;

// Mirrors PCT-EXE-0022's Input/Output shape. Deliberately primitive (not
// Runtime.Execution.ExecutionResult) so ConversationManagement has no
// dependency on the Execution namespace.
public sealed record DispatchOutcomeSummary(bool Success, string? ExecutedCapabilityId, IReadOnlyList<string> Diagnostics);

public sealed record HandoffRequest(RoutingResult RoutingResult, DispatchOutcomeSummary? DispatchOutcome);

public sealed record HandoffResult(string PresentedResult, string? SuggestedNextStep);
