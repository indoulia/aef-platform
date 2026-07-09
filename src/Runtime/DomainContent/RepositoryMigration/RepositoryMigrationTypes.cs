// Mirrors PCT-EXE-0034/0035/0036 exactly.
using Runtime.Execution;

namespace Runtime.DomainContent.RepositoryMigration;

// PCT-EXE-0034
public sealed record TransformationClassificationRequest(string Goal, RepositoryExecutionContext RepositoryContext);
public sealed record TransformationClassificationResult(
    string TransformationClassification,
    string CurrentState,
    string TargetState,
    IReadOnlyList<string> OpenItems);

// PCT-EXE-0035
public sealed record MigrationPhase(int PhaseNumber, string Description, string RevertBoundary);
public sealed record MigrationStrategyRequest(string TransformationClassification, string CurrentState, string TargetState, RepositoryExecutionContext RepositoryContext);
public sealed record MigrationStrategyResult(
    IReadOnlyList<MigrationPhase> Phases,
    string DependencyAndBreakingChangeAnalysis,
    string CompatibilityAnalysis,
    IReadOnlyList<string> OpenItems);

// PCT-EXE-0036 — field names mirror SCH-DEL-0003's own "Specification Type:
// Migration" conditional-section table exactly, so a conforming instance
// can be assembled mechanically.
public sealed record MigrationSpecification(
    string SpecificationType,
    string Goal,
    string TransformationClassification,
    string CurrentState,
    string TargetState,
    IReadOnlyList<MigrationPhase> Phases,
    string DependencyAndBreakingChangeAnalysis,
    string CompatibilityAnalysis,
    string RollbackPlan,
    IReadOnlyList<string> DecomposedStories);
public sealed record MigrationSpecificationAssemblyRequest(
    string Goal,
    string TransformationClassification,
    string CurrentState,
    string TargetState,
    IReadOnlyList<MigrationPhase> Phases,
    string DependencyAndBreakingChangeAnalysis,
    string CompatibilityAnalysis,
    IReadOnlyList<string> OpenItems);
public sealed record MigrationSpecificationAssemblyResult(MigrationSpecification Specification, string ImplementationReadiness, string ApprovalSummary, string Status, IReadOnlyList<string> OpenItems);
