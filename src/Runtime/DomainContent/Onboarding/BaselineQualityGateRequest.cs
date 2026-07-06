namespace Runtime.DomainContent.Onboarding;

// Input to SKL-EXE-0017: S20's Registration outcome (must already be
// Registered), the project's greenfield/existing classification, the
// baseline Quality Gate run's raw results, and any exception rationale
// already drafted for a failing gate.
public sealed record BaselineQualityGateRequest(
    RegistrationOutcome Registration,
    ProjectKind ProjectKind,
    IReadOnlyList<QualityGateResult> GateResults,
    IReadOnlyDictionary<string, string> ExceptionRationales);
