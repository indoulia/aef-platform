// Mirrors PCT-EXE-0023/0024/0025 exactly.
using Runtime.Execution;

namespace Runtime.DomainContent.EngineeringPlanning;

// PCT-EXE-0023
public sealed record DiscoveryFindings(IReadOnlyList<string> ExistingCapability, IReadOnlyList<string> Gaps);
public sealed record DiscoveryRequest(string Goal, RepositoryExecutionContext RepositoryContext);
public sealed record DiscoveryResult(string Status, DiscoveryFindings? Findings, IReadOnlyList<string>? ClarificationQuestions)
{
    public static DiscoveryResult Complete(DiscoveryFindings findings) => new("discovery_complete", findings, null);
    public static DiscoveryResult NeedsClarification(IReadOnlyList<string> questions) => new("needs_clarification", null, questions);
}

// PCT-EXE-0024 — field names mirror SCH-DEL-0003's own "Always-required
// sections" table exactly, so a conforming instance can be assembled
// mechanically. OpenItems names every section this Behavior could not
// resolve without real judgment - never silently blank (PCT-EXE-0024's
// own postcondition).
public sealed record SpecificationDraft(
    string SpecificationType,
    string GoalProblemStatement,
    string SuccessCriteria,
    string ExistingCapabilityAnalysis,
    string FunctionalRequirements,
    string OutOfScope,
    string Risks,
    IReadOnlyList<string> Assumptions,
    string AcceptanceCriteria);
public sealed record DraftingRequest(string Goal, DiscoveryFindings DiscoveryFindings);
public sealed record DraftingResult(SpecificationDraft SpecificationDraft, IReadOnlyList<string> AlignmentFindings, IReadOnlyList<string> OpenItems);

// PCT-EXE-0025
public sealed record ReadinessRequest(SpecificationDraft SpecificationDraft, IReadOnlyList<string> AlignmentFindings, IReadOnlyList<string> OpenItems);
public sealed record ReadinessResult(string ImplementationReadiness, string ApprovalSummary, string Status);
