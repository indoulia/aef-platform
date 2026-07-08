using System.Diagnostics;
using Runtime.Bootstrapping;
using Runtime.Context;
using Runtime.DomainContent.ConversationManagement;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.DomainContent.EngineeringPlanning;
using Runtime.DomainContent.RepositoryIntelligence;
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
            "WFL-DEL-0006" => FromEngineeringPlanning(request),
            "WFL-DEL-0008" => FromRepositoryIntelligence(request),
            "REPO-VALIDATION" => FromRepositoryValidation(request),
            _ => new ExecutionResult(false, null, null, new[] { $"No executable dispatch target for capability '{routingResult.TargetCapabilityId}'." }, null, TimeSpan.Zero, null, null),
        };
    }

    private static RepositoryExecutionContext RequireRepositoryContext(EngineeringRequest request)
    {
        if (request.Payload is not null
            && request.Payload.TryGetValue("RepositoryName", out var nameObj) && nameObj is string name
            && request.Payload.TryGetValue("RepositoryRoot", out var rootObj) && rootObj is string root)
        {
            return RepositoryContextFactory.Create(name, root);
        }

        return new RepositoryExecutionContext(string.Empty, string.Empty, false, false, null, null, null);
    }

    private ExecutionResult FromEngineeringPlanning(EngineeringRequest request)
    {
        var repositoryContext = RequireRepositoryContext(request);
        var goal = request.Payload is not null && request.Payload.TryGetValue("Goal", out var goalObj) && goalObj is string goalText
            ? goalText
            : request.UserRequest;

        var discovery = InvokeSkill<DiscoveryResult>("SKL-EXE-0023", "DiscoveryRequest", new DiscoveryRequest(goal, repositoryContext), request.ProjectProfileId, "Planning-Discovery");
        if (discovery is null || discovery.Status == "needs_clarification")
        {
            var diagnostics = discovery?.ClarificationQuestions ?? new[] { "SKL-EXE-0023 is not invocable." };
            return new ExecutionResult(false, "WFL-DEL-0006", "AGT-EXE-0012", diagnostics, null, TimeSpan.Zero, null, null);
        }

        var drafting = InvokeSkill<DraftingResult>("SKL-EXE-0024", "DraftingRequest", new DraftingRequest(goal, discovery.Findings!), request.ProjectProfileId, "Planning-Drafting");
        if (drafting is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0006", "AGT-EXE-0012", new[] { "SKL-EXE-0024 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var readiness = InvokeSkill<ReadinessResult>("SKL-EXE-0025", "ReadinessRequest", new ReadinessRequest(drafting.SpecificationDraft, drafting.AlignmentFindings, drafting.OpenItems), request.ProjectProfileId, "Planning-Readiness");
        if (readiness is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0006", "AGT-EXE-0012", new[] { "SKL-EXE-0025 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var diagnosticsList = drafting.OpenItems.Concat(drafting.AlignmentFindings).ToList();
        return new ExecutionResult(true, "WFL-DEL-0006", "AGT-EXE-0012", diagnosticsList, new[] { readiness.ImplementationReadiness }, TimeSpan.Zero, readiness.ApprovalSummary, "Awaiting Business Approval.",
            RepositoryAnalyzed: repositoryContext.RepositoryName);
    }

    private ExecutionResult FromRepositoryIntelligence(EngineeringRequest request)
    {
        var repositoryContext = RequireRepositoryContext(request);
        var requestScope = request.Payload is not null && request.Payload.TryGetValue("RequestScope", out var scopeObj) && scopeObj is string scopeText
            ? scopeText
            : request.UserRequest;

        var understanding = InvokeSkill<RepositoryUnderstandingResult>("SKL-EXE-0030", "RepositoryUnderstandingRequest", new RepositoryUnderstandingRequest(requestScope, repositoryContext), request.ProjectProfileId, "RepoIntel-Understanding");
        var codeIntelligence = InvokeSkill<CodeIntelligenceResult>("SKL-EXE-0031", "CodeIntelligenceRequest", new CodeIntelligenceRequest(requestScope, repositoryContext), request.ProjectProfileId, "RepoIntel-CodeIntelligence");

        DiagnosticsResult? diagnostics = null;
        if (request.Payload is not null && request.Payload.TryGetValue("ReportedProblem", out var problemObj) && problemObj is string problem)
        {
            var evidenceSources = request.Payload.TryGetValue("EvidenceSources", out var evidenceObj) && evidenceObj is IReadOnlyList<string> sources
                ? sources
                : Array.Empty<string>();
            diagnostics = InvokeSkill<DiagnosticsResult>("SKL-EXE-0032", "DiagnosticsRequest", new DiagnosticsRequest(problem, evidenceSources), request.ProjectProfileId, "RepoIntel-Diagnostics");
        }

        var combined = new CombinedIntelligenceFindings(understanding, codeIntelligence, diagnostics);
        var synthesis = InvokeSkill<RecommendationSynthesisResult>("SKL-EXE-0033", "CombinedIntelligenceFindings", combined, request.ProjectProfileId, "RepoIntel-Synthesis");

        if (synthesis is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0008", "AGT-EXE-0015", new[] { "SKL-EXE-0033 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var diagnosticsSummary = synthesis.ReportInstance.Findings.Select(f => $"[{f.Class}] {f.Area}: {f.Summary}").ToList();
        var nextStep = synthesis.RequiresHumanApproval ? "Awaiting Human Approval on Root Cause/Architecture/Risk findings." : "No further action needed.";

        return new ExecutionResult(true, "WFL-DEL-0008", "AGT-EXE-0015", diagnosticsSummary, new[] { understanding?.Answer ?? "n/a" }, TimeSpan.Zero, synthesis.ReportInstance.ReportType, nextStep,
            RepositoryAnalyzed: repositoryContext.RepositoryName);
    }

    private ExecutionResult FromRepositoryValidation(EngineeringRequest request)
    {
        var repositoryContext = RequireRepositoryContext(request);
        var outcome = InvokeSkill<ValidationOutcome>("REPO-VALIDATION", "RepositoryContext", repositoryContext, request.ProjectProfileId, "RepositoryValidation");

        if (outcome is null)
        {
            return new ExecutionResult(false, null, "AGT-EXE-0010", new[] { "REPO-VALIDATION is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var diagnostics = outcome.IsEscalated ? new[] { outcome.EscalationReason! } : Array.Empty<string>();
        return new ExecutionResult(!outcome.IsEscalated, null, "AGT-EXE-0010", diagnostics, null, TimeSpan.Zero, null, null,
            RepositoryAnalyzed: repositoryContext.RepositoryName, TestsExecuted: !outcome.IsEscalated);
    }

    private T? InvokeSkill<T>(string skillId, string contentKey, object payload, string projectProfileId, string tierLabel) where T : class
    {
        var skillContext = new RuntimeExecutionContext(
            tierLabel + "-" + Guid.NewGuid().ToString("N"),
            new Tier0Context(Array.Empty<string>(), Array.Empty<string>()),
            projectProfileId,
            tierLabel,
            new Dictionary<string, object> { [contentKey] = payload });

        var invocation = _runtime.SkillHost.Invoke(skillContext, skillId);
        return invocation.Outcome == SkillInvocationOutcome.Invoked ? invocation.Output as T : null;
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
