namespace Runtime.DomainContent.Onboarding;

// A single onboarding-time exception, per docs/discovery/composition/
// 04-project-onboarding.md §2 Step 8: a gap the project is not closing
// immediately, recorded with a rationale - never silently skipped.
public sealed record ExceptionRecord(string GateId, string Rationale);
