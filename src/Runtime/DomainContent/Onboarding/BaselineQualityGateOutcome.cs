namespace Runtime.DomainContent.Onboarding;

public enum BaselineQualityGateStatus
{
    Completed,
    Escalated,
}

// SKL-EXE-0017's two declared outputs (framework/execution/skills/
// baseline-quality-gate-and-exception-recording.md's Behavior): a
// Completed baseline run with every gap either passing or carrying a
// recorded exception, or an Escalation when a gap has no rationale, or the
// project is not yet Registered - never a silently-unaccounted-for gap.
public sealed record BaselineQualityGateOutcome(
    BaselineQualityGateStatus Status,
    ProjectKind? ProjectKind,
    IReadOnlyList<string>? PassingGates,
    IReadOnlyList<ExceptionRecord>? RecordedExceptions,
    string? EscalationReason)
{
    public static BaselineQualityGateOutcome Completed(ProjectKind projectKind, IReadOnlyList<string> passingGates, IReadOnlyList<ExceptionRecord> recordedExceptions) =>
        new(BaselineQualityGateStatus.Completed, projectKind, passingGates, recordedExceptions, EscalationReason: null);

    public static BaselineQualityGateOutcome Escalated(string reason) =>
        new(BaselineQualityGateStatus.Escalated, null, null, null, reason);
}
