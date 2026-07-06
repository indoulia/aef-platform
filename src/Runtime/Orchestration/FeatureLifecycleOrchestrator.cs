using Kernel.Governance;
using Runtime.Context;
using Runtime.Hosts.Strategy;

namespace Runtime.Orchestration;

// The concrete realization of ORC-EXE-0001 (framework/execution/agents/
// orchestrator-implementation.md) operating WFL-DEL-0001 (framework/delivery/
// workflows/feature-lifecycle.md) specifically, per D10 §1.2's "Select and
// start a Workflow instance" / "Allocate an Agent to a step" responsibilities
// (docs/discovery/orchestration/01-orchestrator-design.md). Scoped to the
// Feature Lifecycle Workflow only (epics/0003-...md's own out-of-scope
// statement excludes WFL-DEL-0002/0003) — this is not a generic
// Workflow-definition interpreter, which no Epic in CU-2 has yet scoped.
public sealed class FeatureLifecycleOrchestrator
{
    private const string OrchestratorDefinitionId = "ORC-EXE-0001";
    private const string SupportedWorkflowId = "WFL-DEL-0001";
    private const string FirstStepAgentId = "AGT-EXE-0002";

    private readonly StrategyHost _strategyHost;

    public FeatureLifecycleOrchestrator(StrategyHost strategyHost) => _strategyHost = strategyHost;

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

    // Human-Approval-Gates-table entry #2 (docs/discovery/orchestration/
    // 09-human-approval-gates.md §1): a Clarification exchange evaluates
    // nothing and produces no Engineering Decision — supplying the missing
    // input resumes the same instance at the same step
    // (07-human-interaction-model.md §4, step 6).
    public GateResumeResult SupplyClarification(StrategyInstanceHandle strategy, string suppliedInput)
    {
        if (strategy.State != SessionState.PausedClarification)
        {
            return GateResumeResult.Rejected(
                $"Cannot supply Clarification input while the instance is '{strategy.State}', not 'PausedClarification'.");
        }

        return strategy.Resume() ? GateResumeResult.Resumed : GateResumeResult.Rejected("Resume was not allowed by the state machine.");
    }

    // Human-Approval-Gates-table entry #3: only a recorded Review Decision —
    // never a plain string — may resume or terminate a Roadmap-Review pause.
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
