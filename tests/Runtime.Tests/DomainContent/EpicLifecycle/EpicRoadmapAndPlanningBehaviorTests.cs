using Runtime.DomainContent.EpicLifecycle;
using Xunit;

namespace Runtime.Tests.DomainContent.EpicLifecycle;

// S17, acceptance criterion 2: given a Clarification-complete Epic file,
// the Agent populates ## Roadmap, pauses for the Roadmap Review (Human
// Approval), and on approval populates ## Plan - exactly as SKL-EXE-0005's
// Behavior specifies, adapted to Epic granularity.
public class EpicRoadmapAndPlanningBehaviorTests
{
    [Fact]
    public void Execute_FirstCall_DraftsRoadmap_AndAwaitsReview()
    {
        var workspace = new EpicWorkspace("epic-under-test");
        var behavior = new EpicRoadmapAndPlanningBehavior(workspace);

        var outcome = (EpicRoadmapOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(EpicRoadmapStatus.AwaitingReview, outcome.Status);
        Assert.NotNull(workspace.Roadmap);
        Assert.Null(workspace.Plan);
    }

    [Fact]
    public void Execute_AfterReadyToPopulatePlan_PopulatesPlan_AndAdvancesStage()
    {
        var workspace = new EpicWorkspace("epic-under-test");
        var behavior = new EpicRoadmapAndPlanningBehavior(workspace) { ReadyToPopulatePlan = true };

        var outcome = (EpicRoadmapOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(EpicRoadmapStatus.PlanPopulated, outcome.Status);
        Assert.NotNull(workspace.Plan);
        Assert.Equal("Planning", workspace.Stage);
    }
}
