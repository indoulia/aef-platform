using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.EpicLifecycle;

// The executable realization of SKL-EXE-0014
// (framework/execution/skills/epic-plan-and-roadmap.md)'s Behavior -
// SKL-EXE-0005's own Behavior steps 1-3, adapted to Epic granularity, per
// epics/0004-...md's S17 acceptance criterion 2. Drafts the Roadmap and
// requests its Review (a Human Approval gate - this Skill drafts but never
// itself authorizes the transition out of it), then, once approved,
// populates the Plan.
public sealed class EpicRoadmapAndPlanningBehavior : IBehavior
{
    private readonly EpicWorkspace _workspace;

    public bool ReadyToPopulatePlan { get; set; }

    public EpicRoadmapAndPlanningBehavior(EpicWorkspace workspace) => _workspace = workspace;

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!ReadyToPopulatePlan)
        {
            _workspace.DraftRoadmap($"Deliver the clarified scope: {_workspace.ProblemStatement}");
            return EpicRoadmapOutcome.AwaitingReview;
        }

        _workspace.PopulatePlan("1. Author Story Decomposition. 2. Submit for Roadmap Review. 3. Await Founder execution authorization.");
        _workspace.AdvanceStage("Planning");
        return EpicRoadmapOutcome.PlanPopulated;
    }
}
