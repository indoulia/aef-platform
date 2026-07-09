// Mirrors PCT-EXE-0045/0046/0047/0048/0049 exactly.
using Runtime.Execution;

namespace Runtime.DomainContent.PlatformLifecycle;

// PCT-EXE-0045
public sealed record OperationClassificationRequest(string Goal, RepositoryExecutionContext RepositoryContext);
public sealed record OperationClassificationResult(string OperationType, IReadOnlyList<string> OpenItems);

// PCT-EXE-0046
public sealed record ReferenceIntegrityFinding(string Citation, string Classification, string Rationale);
public sealed record RegistryAndReferenceIntegrityDiagnosisRequest(RepositoryExecutionContext RepositoryContext);
public sealed record RegistryAndReferenceIntegrityDiagnosisResult(IReadOnlyList<ReferenceIntegrityFinding> Findings, IReadOnlyList<string> OpenItems);

// PCT-EXE-0047
public sealed record ArtifactPreservationClassificationRequest(string Scope);
public sealed record ArtifactPreservationClassificationResult(string Classification, string Rationale);

// PCT-EXE-0048
public sealed record SnapshotAndRestorePlanningRequest(string OperationType, RepositoryExecutionContext RepositoryContext, string? TargetGitRef);
public sealed record SnapshotAndRestorePlanningResult(IReadOnlyList<string> Details, IReadOnlyList<string> OpenItems);

// PCT-EXE-0049 — field names mirror SCH-DEL-0007's own "Platform Lifecycle
// Operation Plan" conditional-section table exactly, so a conforming
// instance can be assembled mechanically.
public sealed record PlatformLifecycleOperationPlan(
    string OperationType,
    string Goal,
    string TargetContext,
    IReadOnlyList<string> Details);
public sealed record PlatformLifecycleOperationPlanAssemblyRequest(
    string Goal,
    string OperationType,
    RepositoryExecutionContext RepositoryContext,
    IReadOnlyList<string> Details,
    IReadOnlyList<string> OpenItems);
public sealed record PlatformLifecycleOperationPlanAssemblyResult(PlatformLifecycleOperationPlan Plan, string ImplementationReadiness, string ApprovalSummary, string Status, IReadOnlyList<string> OpenItems);
