using System.Diagnostics;
using Runtime.Bootstrapping;
using Runtime.Context;
using Runtime.DomainContent.ConversationManagement;
using Runtime.Hosts.Agent;
using Runtime.Orchestration;

namespace Runtime.Execution;

// The P2 composition point: routes a raw EngineeringRequest through
// SKL-EXE-0021 (SkillHost), dispatches to whichever existing, unmodified
// capability the routing result names, runs SKL-EXE-0022, and returns a
// structured ExecutionResult. Builds FeatureLifecycleOrchestrator/
// EpicLifecycleOrchestrator itself from the already-booted RuntimeHandle's
// StrategyHost, exactly as every existing test already does - RuntimeHandle
// itself is not modified to expose them.
public sealed class EngineeringExecutionPipeline
{
    private readonly RuntimeHandle _runtime;
    private readonly FeatureLifecycleOrchestrator _featureOrchestrator;
    private readonly EpicLifecycleOrchestrator _epicOrchestrator;

    public EngineeringExecutionPipeline(RuntimeHandle runtime)
    {
        _runtime = runtime;
        _featureOrchestrator = new FeatureLifecycleOrchestrator(runtime.StrategyHost);
        _epicOrchestrator = new EpicLifecycleOrchestrator(runtime.StrategyHost);
    }

    public ExecutionResult Execute(EngineeringRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var conversation = ConversationContext.New(Guid.NewGuid().ToString("N"));
        var executionContext = new EngineeringExecutionContext { Conversation = conversation, Request = request };

        executionContext.State = EngineeringExecutionState.Routing;
        var routingResult = Route(request, conversation);

        if (routingResult.Outcome == RoutingOutcome.NeedsClarification)
        {
            executionContext.State = EngineeringExecutionState.NeedsClarification;
            executionContext.Diagnostics.AddRange(routingResult.ClarificationQuestions ?? Array.Empty<string>());
            var handoff = Handoff(routingResult, dispatchOutcome: null);
            stopwatch.Stop();
            return new ExecutionResult(
                Success: false, null, null, executionContext.Diagnostics, null, stopwatch.Elapsed,
                handoff.PresentedResult, handoff.SuggestedNextStep);
        }

        executionContext.State = EngineeringExecutionState.Dispatching;
        var dispatch = Dispatch(routingResult, request);
        executionContext.SelectedWorkflowId = dispatch.ExecutedWorkflowId;
        executionContext.SelectedAgentId = dispatch.ExecutedAgentId;
        executionContext.Diagnostics.AddRange(dispatch.Diagnostics);
        executionContext.State = dispatch.Success ? EngineeringExecutionState.Completed : EngineeringExecutionState.Failed;

        var summary = new DispatchOutcomeSummary(dispatch.Success, routingResult.TargetCapabilityId, dispatch.Diagnostics);
        var finalHandoff = Handoff(routingResult, summary);
        stopwatch.Stop();

        return dispatch with { Duration = stopwatch.Elapsed, PresentedResult = finalHandoff.PresentedResult, SuggestedNextStep = finalHandoff.SuggestedNextStep };
    }

    private RoutingResult Route(EngineeringRequest request, ConversationContext conversation)
    {
        var routingContext = new RuntimeExecutionContext(
            "routing-" + conversation.ConversationId,
            new Tier0Context(Array.Empty<string>(), Array.Empty<string>()),
            request.ProjectProfileId,
            "Routing",
            new Dictionary<string, object> { ["RoutingRequest"] = new RoutingRequest(request.UserRequest, conversation) });

        var invocation = _runtime.SkillHost.Invoke(routingContext, "SKL-EXE-0021");
        return invocation.Outcome == SkillInvocationOutcome.Invoked
            ? (RoutingResult)invocation.Output!
            : RoutingResult.NeedsClarification(new[] { invocation.Reason ?? "SKL-EXE-0021 is not invocable." });
    }

    private HandoffResult Handoff(RoutingResult routingResult, DispatchOutcomeSummary? dispatchOutcome)
    {
        var handoffContext = new RuntimeExecutionContext(
            "handoff-" + Guid.NewGuid().ToString("N"),
            new Tier0Context(Array.Empty<string>(), Array.Empty<string>()),
            "default-profile",
            "Handoff",
            new Dictionary<string, object> { ["HandoffRequest"] = new HandoffRequest(routingResult, dispatchOutcome) });

        var invocation = _runtime.SkillHost.Invoke(handoffContext, "SKL-EXE-0022");
        return invocation.Outcome == SkillInvocationOutcome.Invoked
            ? (HandoffResult)invocation.Output!
            : new HandoffResult(invocation.Reason ?? "SKL-EXE-0022 is not invocable.", null);
    }

    private ExecutionResult Dispatch(RoutingResult routingResult, EngineeringRequest request)
    {
        return routingResult.TargetCapabilityId switch
        {
            "WFL-DEL-0001" => FromOrchestration(_featureOrchestrator.Start(new ManualTrigger("WFL-DEL-0001", request.Payload)), "WFL-DEL-0001"),
            "WFL-DEL-0004" => FromOrchestration(_epicOrchestrator.Start(new ManualTrigger("WFL-DEL-0004", request.Payload)), "WFL-DEL-0004"),
            "SKL-EXE-0018" => FromDeliveryExecution(request),
            _ => new ExecutionResult(false, null, null, new[] { $"No executable dispatch target for capability '{routingResult.TargetCapabilityId}'." }, null, TimeSpan.Zero, null, null),
        };
    }

    private static ExecutionResult FromOrchestration(OrchestrationStartResult result, string workflowId)
    {
        var success = result.Outcome == OrchestrationStartOutcome.Started;
        var diagnostics = success ? Array.Empty<string>() : new[] { result.Reason ?? "Orchestrator rejected the trigger." };
        return new ExecutionResult(success, success ? workflowId : null, null, diagnostics, null, TimeSpan.Zero, null, null);
    }

    private ExecutionResult FromDeliveryExecution(EngineeringRequest request)
    {
        var payload = request.Payload ?? new Dictionary<string, object>();
        var skillContext = new RuntimeExecutionContext(
            "delivery-" + Guid.NewGuid().ToString("N"),
            new Tier0Context(Array.Empty<string>(), Array.Empty<string>()),
            request.ProjectProfileId,
            "Implementation",
            payload);

        var invocation = _runtime.SkillHost.Invoke(skillContext, "SKL-EXE-0018");
        if (invocation.Outcome != SkillInvocationOutcome.Invoked)
        {
            return new ExecutionResult(false, null, "AGT-EXE-0010", new[] { invocation.Reason ?? "SKL-EXE-0018 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var outcome = (DomainContent.DeliveryExecution.ImplementationOutcome)invocation.Output!;
        var diagnostics = outcome.IsEscalated ? new[] { outcome.EscalationReason! } : Array.Empty<string>();
        var validationResults = outcome.IsEscalated ? null : new[] { $"Verified commits: {string.Join(", ", outcome.LogEntry!.VerifiedCommitShas)}" };
        return new ExecutionResult(!outcome.IsEscalated, null, "AGT-EXE-0010", diagnostics, validationResults, TimeSpan.Zero, null, null);
    }
}
