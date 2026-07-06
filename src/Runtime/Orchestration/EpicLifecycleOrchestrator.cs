using Kernel.Governance;
using Runtime.Context;
using Runtime.Hosts.Strategy;

namespace Runtime.Orchestration;

// The concrete realization of ORC-EXE-0001 operating WFL-DEL-0004
// (framework/delivery/workflows/epic-lifecycle.md) - the Epic-scoped
// sibling of FeatureLifecycleOrchestrator, mirroring its structure exactly
// and reusing StrategyHost/SessionTransitions/ReviewDecision/
// GateResumeResult/OrchestrationStartResult verbatim, unmodified
// (epics/0004-...md Roadmap: "wired into Epic 0003's already-proven
// ORC-EXE-0001-driven state machine and gate routing, without modifying
// either"). FeatureLifecycleOrchestrator's own file is untouched by this
// Story (confirmed by inspection) - this is a new, parallel class, not an
// edit to it.
public sealed class EpicLifecycleOrchestrator
{
    private const string OrchestratorDefinitionId = "ORC-EXE-0001";
    private const string SupportedWorkflowId = "WFL-DEL-0004";
    private const string FirstStepAgentId = "AGT-EXE-0006";

    private readonly StrategyHost _strategyHost;

    public EpicLifecycleOrchestrator(StrategyHost strategyHost) => _strategyHost = strategyHost;

    public OrchestrationStartResult Start(ManualTrigger trigger)
    {
        if (trigger.WorkflowId != SupportedWorkflowId)
        {
            return OrchestrationStartResult.Rejected(
                $"This Orchestrator realizes '{SupportedWorkflowId}' only; '{trigger.WorkflowId}' is out of this Epic's scope.");
        }

        var seed = new ContextSeed(
            new Tier0Context(Array.Empty<string>(), Array.Empty<string>()),
            ProjectProfileId: "default-profile",
            CurrentStepId: "Intake",
            Tier3SkillContent: trigger.InitialInput);

        var activation = _strategyHost.Activate(new ActivationRequest(OrchestratorDefinitionId), seed);
        if (activation.Outcome != StrategyActivationOutcome.Created)
        {
            return OrchestrationStartResult.Rejected(activation.Reason ?? "Orchestrator Strategy was not Activated.");
        }

        var strategy = activation.Handle!;
        var firstStep = strategy.AllocateStep(FirstStepAgentId, seed);
        if (firstStep.Outcome != Runtime.Hosts.Agent.AgentActivationOutcome.Created)
        {
            return OrchestrationStartResult.Rejected(firstStep.Reason ?? "The first step's Agent was not Activated.");
        }

        return OrchestrationStartResult.Started(strategy, firstStep.Handle!);
    }

    public GateResumeResult SupplyClarification(StrategyInstanceHandle strategy, string suppliedInput)
    {
        if (strategy.State != SessionState.PausedClarification)
        {
            return GateResumeResult.Rejected(
                $"Cannot supply Clarification input while the instance is '{strategy.State}', not 'PausedClarification'.");
        }

        return strategy.Resume() ? GateResumeResult.Resumed : GateResumeResult.Rejected("Resume was not allowed by the state machine.");
    }

    public GateResumeResult ResolveRoadmapReview(StrategyInstanceHandle strategy, ReviewDecision decision)
    {
        if (strategy.State != SessionState.PausedApproval)
        {
            return GateResumeResult.Rejected(
                $"Cannot resolve a Roadmap Review while the instance is '{strategy.State}', not 'PausedApproval'.");
        }

        if (decision.Approved)
        {
            return strategy.Resume() ? GateResumeResult.Resumed : GateResumeResult.Rejected("Resume was not allowed by the state machine.");
        }

        return strategy.Terminate() ? GateResumeResult.Terminated : GateResumeResult.Rejected("Terminate was not allowed by the state machine.");
    }
}
