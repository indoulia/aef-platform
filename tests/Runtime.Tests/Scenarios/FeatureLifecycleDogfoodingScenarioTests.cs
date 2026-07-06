using Kernel.Governance;
using Kernel.Registry;
using Runtime.Bootstrapping;
using Runtime.DomainContent.FeatureLifecycle;
using Runtime.Hosts.Agent;
using Runtime.Hosts.Skill;
using Runtime.Hosts.Strategy;
using Runtime.Orchestration;
using Xunit;

namespace Runtime.Tests.Scenarios;

// S15 (epics/0003-feature-lifecycle-dogfooding.md) — Epic 0003's own Definition
// of Done #3, in full: drive one real Feature through Intake -> Clarification
// -> Roadmap (Review) -> Planning, exclusively through ORC-EXE-0001's,
// WFL-DEL-0001's, and AGT-EXE-0002's public behavior, pausing only at the two
// Human-Approval-Gates entries #2 and #3, with a human supplying the
// Clarification answer and rendering the Roadmap Review Decision. This is the
// first time any real, non-synthetic D9 content is exercised end-to-end.
public class FeatureLifecycleDogfoodingScenarioTests
{
    private const string SkillIntakeId = "SKL-EXE-0004";
    private const string SkillRoadmapId = "SKL-EXE-0005";
    private const string AgentId = "AGT-EXE-0002";
    private const string OrchestratorId = "ORC-EXE-0001";
    private const string WorkflowId = "WFL-DEL-0001";

    [Fact]
    public void RegisterActivateIntakeClarifyRoadmapReviewPlan_EndToEnd()
    {
        var workspace = new FeatureWorkspace("real-feature-under-test");
        var intakeBehavior = new IntakeAndClarificationBehavior(workspace, "Add CSV export to the reporting page.", "Reduce support tickets asking for raw data.");
        var roadmapBehavior = new RoadmapAndPlanningBehavior(workspace);

        var runtime = RuntimeBootstrapper.Boot(
            new RuntimeConfiguration(PolicyResolver: new AllowAllPolicyResolver()),
            behaviors: new Dictionary<string, IBehavior>
            {
                [SkillIntakeId] = intakeBehavior,
                [SkillRoadmapId] = roadmapBehavior,
            });

        foreach (var (id, domain) in new[] { (SkillIntakeId, "Execution"), (SkillRoadmapId, "Execution"), (AgentId, "Execution"), (OrchestratorId, "Execution") })
        {
            Assert.Equal(RegisterOutcome.Success, runtime.Kernel.Registry.Register(new DefinitionSubmission(id, domain)).Outcome);
        }

        var orchestrator = new FeatureLifecycleOrchestrator(runtime.StrategyHost);
        var started = orchestrator.Start(new ManualTrigger(WorkflowId, InitialInput: null));
        Assert.Equal(OrchestrationStartOutcome.Started, started.Outcome);
        var strategy = started.Strategy!;
        var agent = started.FirstStepAgent!;

        // Intake: first invocation finds an unresolved ambiguity and pauses.
        var firstIntake = (IntakeOutcome)agent.InvokeSkill(SkillIntakeId).Output!;
        Assert.Equal(IntakeStatus.PendingClarification, firstIntake.Status);
        Assert.True(strategy.PauseForClarification());
        Assert.Equal(SessionState.PausedClarification, strategy.State);

        // A human supplies the missing input; the same instance resumes at the same step.
        intakeBehavior.SuppliedAnswer = "All authenticated users with the 'Reporting Viewer' role.";
        var clarificationResume = orchestrator.SupplyClarification(strategy, intakeBehavior.SuppliedAnswer);
        Assert.Equal(GateResumeOutcome.Resumed, clarificationResume.Outcome);
        Assert.Equal(SessionState.Running, strategy.State);

        // Intake, re-invoked: now resolved.
        var secondIntake = (IntakeOutcome)agent.InvokeSkill(SkillIntakeId).Output!;
        Assert.Equal(IntakeStatus.Resolved, secondIntake.Status);
        Assert.Equal("Clarification", workspace.Stage);

        // Roadmap: drafts, then pauses for Review.
        var roadmapDraft = (RoadmapOutcome)agent.InvokeSkill(SkillRoadmapId).Output!;
        Assert.Equal(RoadmapStatus.AwaitingReview, roadmapDraft.Status);
        Assert.True(strategy.PauseForApproval());
        Assert.Equal(SessionState.PausedApproval, strategy.State);
        Assert.NotNull(workspace.Roadmap);

        // A reviewer approves; only a recorded Review Decision resumes it.
        var reviewResume = orchestrator.ResolveRoadmapReview(strategy, new ReviewDecision(Approved: true, Reason: "Scope is clear and small."));
        Assert.Equal(GateResumeOutcome.Resumed, reviewResume.Outcome);
        Assert.Equal(SessionState.Running, strategy.State);

        // Roadmap skill, re-invoked after approval: populates the Plan.
        roadmapBehavior.ReadyToPopulatePlan = true;
        var planResult = (RoadmapOutcome)agent.InvokeSkill(SkillRoadmapId).Output!;
        Assert.Equal(RoadmapStatus.PlanPopulated, planResult.Status);

        // The Feature's own accumulated sections are populated exactly as
        // SKL-EXE-0004/0005's Behavior sections specify.
        Assert.Equal("Planning", workspace.Stage);
        Assert.Equal("Add CSV export to the reporting page.", workspace.ProblemStatement);
        Assert.Single(workspace.ClarificationLog);
        Assert.Equal("All authenticated users with the 'Reporting Viewer' role.", workspace.ClarificationLog[0].Answer);
        Assert.NotNull(workspace.Roadmap);
        Assert.NotNull(workspace.Plan);
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }
}
