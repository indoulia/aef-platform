using Runtime.DomainContent.EpicLifecycle;
using Xunit;

namespace Runtime.Tests.DomainContent.EpicLifecycle;

// S17 (epics/0004-epic-granularity-autonomous-lifecycle.md), acceptance
// criterion 1: given an Epic-shaped file with only a header populated, the
// Agent populates ## Intake and ## Clarification Log exactly as
// SKL-EXE-0004's Behavior specifies, adapted to the Epic header shape
// (Epic Name / Stage / Corresponds To / Milestone / External Tracker
// Reference, per epics/0001-0003's own established shape).
public class EpicIntakeAndClarificationBehaviorTests
{
    [Fact]
    public void Execute_WithoutSuppliedAnswer_PopulatesIntake_AndPausesForClarification()
    {
        var workspace = new EpicWorkspace("epic-under-test");
        var behavior = new EpicIntakeAndClarificationBehavior(workspace, "Problem statement.", "Motivation.");

        var outcome = (EpicIntakeOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(EpicIntakeStatus.PendingClarification, outcome.Status);
        Assert.NotNull(outcome.UnresolvedQuestion);
        Assert.Equal("Problem statement.", workspace.ProblemStatement);
        Assert.Equal("Motivation.", workspace.Motivation);
        Assert.Equal("Intake", workspace.Stage);
    }

    [Fact]
    public void Execute_WithSuppliedAnswer_ResolvesClarification_AndAdvancesStage()
    {
        var workspace = new EpicWorkspace("epic-under-test");
        var behavior = new EpicIntakeAndClarificationBehavior(workspace, "Problem statement.", "Motivation.")
        {
            SuppliedAnswer = "The declared candidate this Epic should target.",
        };

        var outcome = (EpicIntakeOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(EpicIntakeStatus.Resolved, outcome.Status);
        Assert.Null(outcome.UnresolvedQuestion);
        Assert.Single(workspace.ClarificationLog);
        Assert.Equal("Clarification", workspace.Stage);
    }
}
