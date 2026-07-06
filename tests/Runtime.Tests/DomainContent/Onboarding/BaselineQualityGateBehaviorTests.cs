using Runtime.DomainContent.Onboarding;
using Xunit;

namespace Runtime.Tests.DomainContent.Onboarding;

// S21 (epics/0006-repository-discovery-and-canonical-onboarding.md) - the
// Baseline Quality Gate & Exception Recording Skill's Behavior. Covers the
// S21 acceptance criterion: given a Registered, workspace-initialized
// project, run the baseline Quality Gate set and classify per the
// greenfield/existing divergence (§3); every gap is either closed or
// carries a recorded exception with rationale - none left unaccounted for.
public class BaselineQualityGateBehaviorTests
{
    private static RegistrationOutcome RegisteredOutcome() =>
        RegistrationOutcome.Registered(
            new ProjectProfileDraft("PPR-PRJ-sample-repo", "sample-repo", Adopted: new[] { "STD-GOV-0001" }, Deferred: Array.Empty<string>()),
            new TechnologyProfileDraft("TPR-PRJ-sample-repo", "dotnet"),
            new FrameworkSyncRecord("2.3.0"));

    [Fact]
    public void GreenfieldProject_AllGatesPass_CompletesWithNoExceptions()
    {
        var request = new BaselineQualityGateRequest(
            RegisteredOutcome(),
            ProjectKind.Greenfield,
            GateResults: new[] { new QualityGateResult("QGT-GOV-0001", Passed: true) },
            ExceptionRationales: new Dictionary<string, string>());
        var behavior = new BaselineQualityGateBehavior(request);

        var outcome = (BaselineQualityGateOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(BaselineQualityGateStatus.Completed, outcome.Status);
        Assert.Equal(ProjectKind.Greenfield, outcome.ProjectKind);
        Assert.Contains("QGT-GOV-0001", outcome.PassingGates!);
        Assert.Empty(outcome.RecordedExceptions!);
        Assert.Null(outcome.EscalationReason);
    }

    [Fact]
    public void ExistingProject_GapsWithRationale_CompletesWithRecordedExceptions()
    {
        var request = new BaselineQualityGateRequest(
            RegisteredOutcome(),
            ProjectKind.Existing,
            GateResults: new[]
            {
                new QualityGateResult("QGT-GOV-0001", Passed: true),
                new QualityGateResult("QGT-GOV-0002", Passed: false),
            },
            ExceptionRationales: new Dictionary<string, string>
            {
                ["QGT-GOV-0002"] = "No prior decision records exist - remediation planned within one quarter.",
            });
        var behavior = new BaselineQualityGateBehavior(request);

        var outcome = (BaselineQualityGateOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(BaselineQualityGateStatus.Completed, outcome.Status);
        Assert.Equal(ProjectKind.Existing, outcome.ProjectKind);
        Assert.Contains("QGT-GOV-0001", outcome.PassingGates!);
        Assert.Single(outcome.RecordedExceptions!);
        Assert.Equal("QGT-GOV-0002", outcome.RecordedExceptions![0].GateId);
        Assert.Equal("No prior decision records exist - remediation planned within one quarter.", outcome.RecordedExceptions[0].Rationale);
    }

    [Fact]
    public void ExistingProject_GapWithoutRationale_Escalates_NeverLeavesUnaccounted()
    {
        var request = new BaselineQualityGateRequest(
            RegisteredOutcome(),
            ProjectKind.Existing,
            GateResults: new[] { new QualityGateResult("QGT-GOV-0002", Passed: false) },
            ExceptionRationales: new Dictionary<string, string>());
        var behavior = new BaselineQualityGateBehavior(request);

        var outcome = (BaselineQualityGateOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(BaselineQualityGateStatus.Escalated, outcome.Status);
        Assert.NotNull(outcome.EscalationReason);
        Assert.Contains("QGT-GOV-0002", outcome.EscalationReason);
        Assert.Null(outcome.PassingGates);
        Assert.Null(outcome.RecordedExceptions);
    }

    [Fact]
    public void UnregisteredProject_Escalates_NeverRunsGates()
    {
        var unregistered = RegistrationOutcome.Failed("Registration conflict.");
        var request = new BaselineQualityGateRequest(
            unregistered,
            ProjectKind.Greenfield,
            GateResults: new[] { new QualityGateResult("QGT-GOV-0001", Passed: true) },
            ExceptionRationales: new Dictionary<string, string>());
        var behavior = new BaselineQualityGateBehavior(request);

        var outcome = (BaselineQualityGateOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(BaselineQualityGateStatus.Escalated, outcome.Status);
        Assert.NotNull(outcome.EscalationReason);
    }
}
