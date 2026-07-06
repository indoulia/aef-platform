using System.Linq;
using Kernel.Governance;
using Kernel.Registry;
using Runtime.Bootstrapping;
using Runtime.DomainContent.EpicLifecycle;
using Runtime.Hosts.Skill;
using Runtime.Hosts.Strategy;
using Runtime.Orchestration;
using Xunit;

namespace Runtime.Tests.Scenarios;

// S18 (epics/0004-epic-granularity-autonomous-lifecycle.md) - Epic 0004's
// own Definition of Done #3, in full: S16 identifies the next-eligible
// Capability Unit against the REAL I5 CU-3/CU-4 table (an intentional test
// of the Ambiguous path - both share CU-1 as their sole dependency, which
// is satisfied); S17's Agent then autonomously carries the Founder-chosen
// candidate's Epic through Intake -> Clarification -> Roadmap (Review) ->
// Planning, pausing only at the two Human-Approval-Gates entries #2 and #3.
// This test proves the MECHANISM end-to-end with a simulated Review
// Decision, exactly as FeatureLifecycleDogfoodingScenarioTests (S15) did
// for D9 content; the REAL, live dogfooding run - producing an actual
// epics/0006-*.md and an actual, non-simulated Founder Roadmap Review
// decision - is performed separately (see epics/0004-...md's Closure
// Report and epics/decisions/0005-s18-dogfooding-wiring-decisions.md).
public class EpicDogfoodingScenarioTests
{
    private const string SkillIntakeId = "SKL-EXE-0013";
    private const string SkillRoadmapId = "SKL-EXE-0014";
    private const string AgentId = "AGT-EXE-0006";
    private const string OrchestratorId = "ORC-EXE-0001";
    private const string WorkflowId = "WFL-DEL-0004";

    [Fact]
    public void NextEligibleItemDiscovery_AgainstRealI5CandidateTable_ReturnsAmbiguous()
    {
        var runtime = RuntimeBootstrapper.Boot(new RuntimeConfiguration(PolicyResolver: new AllowAllPolicyResolver()));
        runtime.Kernel.Registry.Register(new DefinitionSubmission(OrchestratorId, "Execution"));
        var behavior = new NextEligibleItemDiscoveryBehavior(runtime.Catalog, EpicCapabilityCandidates.I5Table);

        var outcome = (DiscoveryOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(DiscoveryStatus.Ambiguous, outcome.Status);
        Assert.Equal(2, outcome.AmbiguousCandidates!.Count);
        Assert.Contains(outcome.AmbiguousCandidates, c => c.Id == "CU-3");
        Assert.Contains(outcome.AmbiguousCandidates, c => c.Id == "CU-4");
    }

    [Fact]
    public void RegisterActivateIntakeClarifyRoadmapReviewPlan_ForFounderChosenCandidate_EndToEnd()
    {
        // Per epics/decisions/0005-s18-dogfooding-wiring-decisions.md's
        // tie-break: CU-3 is the Founder-chosen candidate for this run.
        var chosenCandidate = EpicCapabilityCandidates.I5Table.Single(c => c.Id == "CU-3");
        var workspace = new EpicWorkspace(chosenCandidate.Name);
        var intakeBehavior = new EpicIntakeAndClarificationBehavior(
            workspace,
            $"Automate {chosenCandidate.Name}, per its own I5 candidate row.",
            "Realize CU-2's own success criterion (c): carry an already-identified Epic through Intake/Clarification/Roadmap/Planning autonomously.");
        var roadmapBehavior = new EpicRoadmapAndPlanningBehavior(workspace);

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

        var orchestrator = new EpicLifecycleOrchestrator(runtime.StrategyHost);
        var started = orchestrator.Start(new ManualTrigger(WorkflowId, InitialInput: null));
        Assert.Equal(OrchestrationStartOutcome.Started, started.Outcome);
        var strategy = started.Strategy!;
        var agent = started.FirstStepAgent!;

        // Intake: first invocation finds an unresolved ambiguity and pauses.
        var firstIntake = (EpicIntakeOutcome)agent.InvokeSkill(SkillIntakeId).Output!;
        Assert.Equal(EpicIntakeStatus.PendingClarification, firstIntake.Status);
        Assert.True(strategy.PauseForClarification());
        Assert.Equal(SessionState.PausedClarification, strategy.State);

        // A human supplies the missing input; the same instance resumes.
        intakeBehavior.SuppliedAnswer = "CU-3, per the Founder-declared tie-break recorded in ADR-0005.";
        var clarificationResume = orchestrator.SupplyClarification(strategy, intakeBehavior.SuppliedAnswer);
        Assert.Equal(GateResumeOutcome.Resumed, clarificationResume.Outcome);
        Assert.Equal(SessionState.Running, strategy.State);

        var secondIntake = (EpicIntakeOutcome)agent.InvokeSkill(SkillIntakeId).Output!;
        Assert.Equal(EpicIntakeStatus.Resolved, secondIntake.Status);
        Assert.Equal("Clarification", workspace.Stage);

        // Roadmap: drafts, then pauses for Review.
        var roadmapDraft = (EpicRoadmapOutcome)agent.InvokeSkill(SkillRoadmapId).Output!;
        Assert.Equal(EpicRoadmapStatus.AwaitingReview, roadmapDraft.Status);
        Assert.True(strategy.PauseForApproval());
        Assert.Equal(SessionState.PausedApproval, strategy.State);
        Assert.NotNull(workspace.Roadmap);

        // Simulated Review Decision (this automated test's own mechanism
        // proof only - the real, live dogfooding run uses an actual,
        // non-simulated Founder decision; see this file's class comment).
        var reviewResume = orchestrator.ResolveRoadmapReview(strategy, new ReviewDecision(Approved: true, Reason: "Mechanism proof - see the real dogfooding run for the actual Founder decision."));
        Assert.Equal(GateResumeOutcome.Resumed, reviewResume.Outcome);
        Assert.Equal(SessionState.Running, strategy.State);

        roadmapBehavior.ReadyToPopulatePlan = true;
        var planResult = (EpicRoadmapOutcome)agent.InvokeSkill(SkillRoadmapId).Output!;
        Assert.Equal(EpicRoadmapStatus.PlanPopulated, planResult.Status);

        Assert.Equal("Planning", workspace.Stage);
        Assert.NotNull(workspace.Roadmap);
        Assert.NotNull(workspace.Plan);
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }
}
