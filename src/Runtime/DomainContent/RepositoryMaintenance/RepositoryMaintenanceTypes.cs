// Mirrors PCT-EXE-0042/0043/0044 exactly.
using Runtime.Execution;

namespace Runtime.DomainContent.RepositoryMaintenance;

// PCT-EXE-0042
public sealed record HealthDimensionFinding(string Dimension, string Classification, string Rationale);
public sealed record RepositoryHealthAssessmentRequest(string Goal, RepositoryExecutionContext RepositoryContext);
public sealed record RepositoryHealthAssessmentResult(IReadOnlyList<HealthDimensionFinding> Findings, IReadOnlyList<string> OpenItems);

// PCT-EXE-0043
public sealed record MaintenanceItem(
    string Description,
    string Severity,
    string Priority,
    string RiskClassification,
    string EffortNote,
    bool SafeToAutomate,
    string? Sequencing,
    string RollbackNote);
public sealed record MaintenancePlanningRequest(IReadOnlyList<HealthDimensionFinding> HealthFindings, RepositoryExecutionContext RepositoryContext);
public sealed record MaintenancePlanningResult(IReadOnlyList<MaintenanceItem> Items, IReadOnlyList<string> OpenItems);

// PCT-EXE-0044 — field names mirror SCH-DEL-0003's own "Specification Type:
// Maintenance" conditional-section table exactly, so a conforming instance
// can be assembled mechanically.
public sealed record MaintenanceSpecification(
    string SpecificationType,
    string Goal,
    IReadOnlyList<HealthDimensionFinding> HealthFindings,
    IReadOnlyList<MaintenanceItem> Items,
    IReadOnlyList<string> DecomposedStories);
public sealed record MaintenanceSpecificationAssemblyRequest(
    string Goal,
    IReadOnlyList<HealthDimensionFinding> HealthFindings,
    IReadOnlyList<MaintenanceItem> Items,
    IReadOnlyList<string> OpenItems);
public sealed record MaintenanceSpecificationAssemblyResult(MaintenanceSpecification Specification, string ImplementationReadiness, string ApprovalSummary, string Status, IReadOnlyList<string> OpenItems);
