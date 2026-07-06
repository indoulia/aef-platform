namespace Runtime.DomainContent.Onboarding;

// One gate's pass/fail result from the baseline Quality Gate run
// (docs/discovery/composition/04-project-onboarding.md §2 Step 7).
public sealed record QualityGateResult(string GateId, bool Passed);
