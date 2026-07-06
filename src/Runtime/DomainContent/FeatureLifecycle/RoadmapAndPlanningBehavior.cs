using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.FeatureLifecycle;

// The executable realization of SKL-EXE-0005
// (framework/execution/skills/feature-plan-and-roadmap.md)'s own Behavior
// steps 1-3: draft the Roadmap and request its Review (a Human Approval
// gate — this Skill drafts but never itself authorizes the transition out
// of it), then, once approved, populate the Plan.
public sealed class RoadmapAndPlanningBehavior : IBehavior
{
    private readonly FeatureWorkspace _workspace;

    public bool ReadyToPopulatePlan { get; set; }

    public RoadmapAndPlanningBehavior(FeatureWorkspace workspace) => _workspace = workspace;

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!ReadyToPopulatePlan)
        {
            _workspace.DraftRoadmap($"Deliver the clarified scope: {_workspace.ProblemStatement}");
            return RoadmapOutcome.AwaitingReview;
        }

        _workspace.PopulatePlan("1. Implement. 2. Test. 3. Document.");
        _workspace.AdvanceStage("Planning");
        return RoadmapOutcome.PlanPopulated;
    }
}
