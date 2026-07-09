using System.Diagnostics;
using Runtime.Bootstrapping;
using Runtime.Context;
using Runtime.DomainContent.ConversationManagement;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.DomainContent.EngineeringPlanning;
using Runtime.DomainContent.RepositoryIntelligence;
using Runtime.DomainContent.PlatformLifecycle;
using Runtime.DomainContent.RepositoryMaintenance;
using Runtime.DomainContent.RepositoryMigration;
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
                handoff.PresentedResult, handoff.SuggestedNextStep, Kind: ExecutionResultKind.NeedsClarification);
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
            "WFL-DEL-0007" => FromEngineeringSpecificationReview(request),
            "WFL-DEL-0005" => FromImplementationValidation(request),
            "WFL-DEL-0009" => FromMigrationPlanning(request),
            "WFL-DEL-0011" => FromMaintenancePlanning(request),
            "WFL-DEL-0012" => FromPlatformLifecycle(request),
            _ => new ExecutionResult(false, null, null, new[] { $"No executable dispatch target for capability '{routingResult.TargetCapabilityId}'." }, null, TimeSpan.Zero, null, null),
        };
    }

    // Returns null (never a fabricated empty context) when the caller
    // didn't supply RepositoryName/RepositoryRoot, so a genuinely missing
    // repository payload surfaces as its own clarification-shaped
    // diagnostic rather than a confusing "repository '' is not readable"
    // downstream (code review: conflating "no repository was specified"
    // with "repository exists but isn't readable" produced misleading
    // diagnostics for a caller who simply forgot the payload keys).
    private static RepositoryExecutionContext? TryGetRepositoryContext(EngineeringRequest request)
    {
        if (request.Payload is not null
            && request.Payload.TryGetValue("RepositoryName", out var nameObj) && nameObj is string name && !string.IsNullOrEmpty(name)
            && request.Payload.TryGetValue("RepositoryRoot", out var rootObj) && rootObj is string root && !string.IsNullOrEmpty(root))
        {
            return RepositoryContextFactory.Create(name, root);
        }

        return null;
    }

    private static ExecutionResult MissingRepositoryContext(string? workflowId, string agentId) =>
        new(false, workflowId, agentId, new[] { "No repository context was supplied - Payload must carry RepositoryName and RepositoryRoot." }, null, TimeSpan.Zero,
            "I need a repository to work with.", "Supply RepositoryName and RepositoryRoot in the request payload and retry.");

    private ExecutionResult FromEngineeringPlanning(EngineeringRequest request)
    {
        var repositoryContext = TryGetRepositoryContext(request);
        if (repositoryContext is null)
        {
            return MissingRepositoryContext("WFL-DEL-0006", "AGT-EXE-0012");
        }

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
            RepositoryAnalyzed: repositoryContext.RepositoryName, FilesChanged: repositoryContext.ChangedPaths);
    }

    // Epic 3 (Repository Migration & Modernization, ai-engineering-framework
    // repo): AGT-EXE-0016 Migration Planning Agent. Deliberately its own
    // dispatch method, not folded into FromEngineeringPlanning - the
    // Migration Planning Agent's own design leaves the composition choice
    // (hand conditional content to Engineering Planner vs. assemble it
    // independently) to whichever Epic builds this; assembling it
    // independently here touches zero of Engineering Planning's own
    // already-frozen, tested code (Minimum Governance Change).
    private ExecutionResult FromMigrationPlanning(EngineeringRequest request)
    {
        var repositoryContext = TryGetRepositoryContext(request);
        if (repositoryContext is null)
        {
            return MissingRepositoryContext("WFL-DEL-0009", "AGT-EXE-0016");
        }

        var goal = request.Payload is not null && request.Payload.TryGetValue("Goal", out var goalObj) && goalObj is string goalText
            ? goalText
            : request.UserRequest;

        var classification = InvokeSkill<TransformationClassificationResult>(
            "SKL-EXE-0034", "TransformationClassificationRequest", new TransformationClassificationRequest(goal, repositoryContext), request.ProjectProfileId, "Migration-Assessment");
        if (classification is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0009", "AGT-EXE-0016", new[] { "SKL-EXE-0034 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var strategy = InvokeSkill<MigrationStrategyResult>(
            "SKL-EXE-0035", "MigrationStrategyRequest",
            new MigrationStrategyRequest(classification.TransformationClassification, classification.CurrentState, classification.TargetState, repositoryContext),
            request.ProjectProfileId, "Migration-Strategy");
        if (strategy is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0009", "AGT-EXE-0016", new[] { "SKL-EXE-0035 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var inheritedOpenItems = classification.OpenItems.Concat(strategy.OpenItems).ToList();
        var assembly = InvokeSkill<MigrationSpecificationAssemblyResult>(
            "SKL-EXE-0036", "MigrationSpecificationAssemblyRequest",
            new MigrationSpecificationAssemblyRequest(goal, classification.TransformationClassification, classification.CurrentState, classification.TargetState,
                strategy.Phases, strategy.DependencyAndBreakingChangeAnalysis, strategy.CompatibilityAnalysis, inheritedOpenItems),
            request.ProjectProfileId, "Migration-Assembly");
        if (assembly is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0009", "AGT-EXE-0016", new[] { "SKL-EXE-0036 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        return new ExecutionResult(true, "WFL-DEL-0009", "AGT-EXE-0016", assembly.OpenItems, new[] { assembly.ImplementationReadiness }, TimeSpan.Zero,
            assembly.ApprovalSummary, "Awaiting Business Approval.",
            RepositoryAnalyzed: repositoryContext.RepositoryName, FilesChanged: repositoryContext.ChangedPaths);
    }

    // Epic 5 (Engineering Maintenance & Repository Hygiene, ai-engineering-
    // framework repo): AGT-EXE-0018 Repository Maintenance Agent. Its own
    // three Skills read, classify, and draft - none writes a file; every
    // actual dependency bump, config edit, or cleanup remains an ordinary
    // Story through the unmodified WFL-DEL-0005, the same boundary Migration
    // Planning already established for itself.
    private ExecutionResult FromMaintenancePlanning(EngineeringRequest request)
    {
        var repositoryContext = TryGetRepositoryContext(request);
        if (repositoryContext is null)
        {
            return MissingRepositoryContext("WFL-DEL-0011", "AGT-EXE-0018");
        }

        var goal = request.Payload is not null && request.Payload.TryGetValue("Goal", out var goalObj) && goalObj is string goalText
            ? goalText
            : request.UserRequest;

        var healthAssessment = InvokeSkill<RepositoryHealthAssessmentResult>(
            "SKL-EXE-0042", "RepositoryHealthAssessmentRequest", new RepositoryHealthAssessmentRequest(goal, repositoryContext), request.ProjectProfileId, "Maintenance-Health");
        if (healthAssessment is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0011", "AGT-EXE-0018", new[] { "SKL-EXE-0042 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var planning = InvokeSkill<MaintenancePlanningResult>(
            "SKL-EXE-0043", "MaintenancePlanningRequest", new MaintenancePlanningRequest(healthAssessment.Findings, repositoryContext), request.ProjectProfileId, "Maintenance-Planning");
        if (planning is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0011", "AGT-EXE-0018", new[] { "SKL-EXE-0043 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var inheritedOpenItems = healthAssessment.OpenItems.Concat(planning.OpenItems).ToList();
        var assembly = InvokeSkill<MaintenanceSpecificationAssemblyResult>(
            "SKL-EXE-0044", "MaintenanceSpecificationAssemblyRequest",
            new MaintenanceSpecificationAssemblyRequest(goal, healthAssessment.Findings, planning.Items, inheritedOpenItems),
            request.ProjectProfileId, "Maintenance-Assembly");
        if (assembly is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0011", "AGT-EXE-0018", new[] { "SKL-EXE-0044 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        return new ExecutionResult(true, "WFL-DEL-0011", "AGT-EXE-0018", assembly.OpenItems, new[] { assembly.ImplementationReadiness }, TimeSpan.Zero,
            assembly.ApprovalSummary, "Awaiting Business Approval.",
            RepositoryAnalyzed: repositoryContext.RepositoryName, FilesChanged: repositoryContext.ChangedPaths);
    }

    // Epic 6 (Platform Lifecycle Management, ai-engineering-framework repo):
    // AGT-EXE-0019 Platform Lifecycle Agent. Its own Scope Boundary is
    // universal across all six operation types - it never itself installs,
    // upgrades, repairs, resets, backs up, or restores anything, only
    // produces a reviewed Plan. Upgrade is the one operation type with a
    // real, live delegation target: this method hands off directly to
    // Migration Planning's own Strategy/Assembly Skills (SKL-EXE-0035/0036),
    // with Transformation Classification forced to "Technology/Framework
    // Upgrade" per Responsibility 3 - Migration's own Classification Skill
    // (SKL-EXE-0034) is deliberately skipped, since this Agent's own
    // classification already determined the operation type. Installation
    // names CU-3's onboarding sequence as the next step rather than
    // invoking it - that sequence has no live dispatch entry on this
    // Pipeline today (confirmed, not assumed).
    private ExecutionResult FromPlatformLifecycle(EngineeringRequest request)
    {
        var repositoryContext = TryGetRepositoryContext(request);
        if (repositoryContext is null)
        {
            return MissingRepositoryContext("WFL-DEL-0012", "AGT-EXE-0019");
        }

        var goal = request.Payload is not null && request.Payload.TryGetValue("Goal", out var goalObj) && goalObj is string goalText
            ? goalText
            : request.UserRequest;

        var classification = InvokeSkill<OperationClassificationResult>(
            "SKL-EXE-0045", "OperationClassificationRequest", new OperationClassificationRequest(goal, repositoryContext), request.ProjectProfileId, "PlatformLifecycle-Classification");
        if (classification is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0012", "AGT-EXE-0019", new[] { "SKL-EXE-0045 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var details = new List<string>();
        var openItems = new List<string>(classification.OpenItems);

        switch (classification.OperationType)
        {
            case "Upgrade":
                var currentState = repositoryContext.TechnologyProfile is { } profile
                    ? $"Current stack, per Repository Intelligence: {string.Join("; ", profile.Technologies.Select(t => t.Name))}."
                    : "No Technology Profile is available - current state requires human input.";
                var forcedClassification = new TransformationClassificationResult("Technology/Framework Upgrade", currentState, "Not determined by automated classification - requires human input.", Array.Empty<string>());

                var strategy = InvokeSkill<MigrationStrategyResult>(
                    "SKL-EXE-0035", "MigrationStrategyRequest",
                    new MigrationStrategyRequest(forcedClassification.TransformationClassification, forcedClassification.CurrentState, forcedClassification.TargetState, repositoryContext),
                    request.ProjectProfileId, "PlatformLifecycle-Upgrade-Strategy");
                if (strategy is null)
                {
                    return new ExecutionResult(false, "WFL-DEL-0012", "AGT-EXE-0019", new[] { "SKL-EXE-0035 is not invocable." }, null, TimeSpan.Zero, null, null);
                }

                var upgradeInheritedOpenItems = forcedClassification.OpenItems.Concat(strategy.OpenItems).ToList();
                var upgradeAssembly = InvokeSkill<MigrationSpecificationAssemblyResult>(
                    "SKL-EXE-0036", "MigrationSpecificationAssemblyRequest",
                    new MigrationSpecificationAssemblyRequest(goal, forcedClassification.TransformationClassification, forcedClassification.CurrentState, forcedClassification.TargetState,
                        strategy.Phases, strategy.DependencyAndBreakingChangeAnalysis, strategy.CompatibilityAnalysis, upgradeInheritedOpenItems),
                    request.ProjectProfileId, "PlatformLifecycle-Upgrade-Assembly");
                if (upgradeAssembly is null)
                {
                    return new ExecutionResult(false, "WFL-DEL-0012", "AGT-EXE-0019", new[] { "SKL-EXE-0036 is not invocable." }, null, TimeSpan.Zero, null, null);
                }

                details.Add($"Delegated to AGT-EXE-0016 Migration Planning Agent: {upgradeAssembly.ApprovalSummary}");
                openItems.AddRange(upgradeAssembly.OpenItems);
                break;

            case "Installation":
                details.Add($"Onboarding prerequisite check: repository is {(repositoryContext.IsReadable ? "readable" : "not readable")}. Hands off to CU-3's existing onboarding sequence for the actual onboarding steps.");
                openItems.Add("CU-3's onboarding sequence has no live dispatch entry on this Pipeline yet - the actual onboarding steps require a human-directed session.");
                break;

            case "Repair":
                var diagnosis = InvokeSkill<RegistryAndReferenceIntegrityDiagnosisResult>(
                    "SKL-EXE-0046", "RegistryAndReferenceIntegrityDiagnosisRequest", new RegistryAndReferenceIntegrityDiagnosisRequest(repositoryContext), request.ProjectProfileId, "PlatformLifecycle-Repair");
                if (diagnosis is null)
                {
                    return new ExecutionResult(false, "WFL-DEL-0012", "AGT-EXE-0019", new[] { "SKL-EXE-0046 is not invocable." }, null, TimeSpan.Zero, null, null);
                }

                details.AddRange(diagnosis.Findings.Select(f => $"[{f.Classification}] {f.Citation}: {f.Rationale}"));
                openItems.AddRange(diagnosis.OpenItems);
                break;

            case "Reset":
                var reset = InvokeSkill<ArtifactPreservationClassificationResult>(
                    "SKL-EXE-0047", "ArtifactPreservationClassificationRequest", new ArtifactPreservationClassificationRequest(goal), request.ProjectProfileId, "PlatformLifecycle-Reset");
                if (reset is null)
                {
                    return new ExecutionResult(false, "WFL-DEL-0012", "AGT-EXE-0019", new[] { "SKL-EXE-0047 is not invocable." }, null, TimeSpan.Zero, null, null);
                }

                details.Add($"[{reset.Classification}] {reset.Rationale}");
                break;

            case "Backup":
            case "Restore":
                var targetGitRef = request.Payload is not null && request.Payload.TryGetValue("TargetGitRef", out var refObj) && refObj is string refText ? refText : null;
                var snapshot = InvokeSkill<SnapshotAndRestorePlanningResult>(
                    "SKL-EXE-0048", "SnapshotAndRestorePlanningRequest", new SnapshotAndRestorePlanningRequest(classification.OperationType, repositoryContext, targetGitRef),
                    request.ProjectProfileId, "PlatformLifecycle-Snapshot");
                if (snapshot is null)
                {
                    return new ExecutionResult(false, "WFL-DEL-0012", "AGT-EXE-0019", new[] { "SKL-EXE-0048 is not invocable." }, null, TimeSpan.Zero, null, null);
                }

                details.AddRange(snapshot.Details);
                openItems.AddRange(snapshot.OpenItems);
                break;
        }

        var assembly = InvokeSkill<PlatformLifecycleOperationPlanAssemblyResult>(
            "SKL-EXE-0049", "PlatformLifecycleOperationPlanAssemblyRequest",
            new PlatformLifecycleOperationPlanAssemblyRequest(goal, classification.OperationType, repositoryContext, details, openItems),
            request.ProjectProfileId, "PlatformLifecycle-Assembly");
        if (assembly is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0012", "AGT-EXE-0019", new[] { "SKL-EXE-0049 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        return new ExecutionResult(true, "WFL-DEL-0012", "AGT-EXE-0019", assembly.OpenItems, new[] { assembly.ImplementationReadiness }, TimeSpan.Zero,
            assembly.ApprovalSummary, "Awaiting Human Approval.",
            RepositoryAnalyzed: repositoryContext.RepositoryName, FilesChanged: repositoryContext.ChangedPaths);
    }

    private ExecutionResult FromRepositoryIntelligence(EngineeringRequest request)
    {
        var repositoryContext = TryGetRepositoryContext(request);
        if (repositoryContext is null)
        {
            return MissingRepositoryContext("WFL-DEL-0008", "AGT-EXE-0015");
        }

        var requestScope = request.Payload is not null && request.Payload.TryGetValue("RequestScope", out var scopeObj) && scopeObj is string scopeText
            ? scopeText
            : request.UserRequest;

        var understanding = InvokeSkill<RepositoryUnderstandingResult>("SKL-EXE-0030", "RepositoryUnderstandingRequest", new RepositoryUnderstandingRequest(requestScope, repositoryContext), request.ProjectProfileId, "RepoIntel-Understanding");
        if (understanding is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0008", "AGT-EXE-0015", new[] { "SKL-EXE-0030 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var codeIntelligence = InvokeSkill<CodeIntelligenceResult>("SKL-EXE-0031", "CodeIntelligenceRequest", new CodeIntelligenceRequest(requestScope, repositoryContext), request.ProjectProfileId, "RepoIntel-CodeIntelligence");
        if (codeIntelligence is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0008", "AGT-EXE-0015", new[] { "SKL-EXE-0031 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

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
        if (diagnostics is not null)
        {
            // SKL-EXE-0032's own evidence chain/confidence/hypotheses were
            // previously computed but never surfaced anywhere in the
            // ExecutionResult - RecommendationSynthesisResult only folds
            // Understanding/CodeIntelligence findings into ReportInstance,
            // so a diagnostic request's own root-cause detail was silently
            // dropped (code review finding).
            diagnosticsSummary.Add($"Root cause confidence: {diagnostics.ConfidenceScore}/10 - {diagnostics.ConfidenceJustification}");
            diagnosticsSummary.AddRange(diagnostics.AlternativeHypotheses.Select(h => $"Alternative hypothesis: {h.Hypothesis} (evidence: {h.SupportingEvidence})"));
        }

        var nextStep = synthesis.ReviewInstance?.Summary
            ?? (synthesis.RequiresHumanApproval ? "Awaiting Human Approval on Root Cause/Architecture/Risk findings." : "No further action needed.");

        return new ExecutionResult(true, "WFL-DEL-0008", "AGT-EXE-0015", diagnosticsSummary, new[] { understanding.Answer }, TimeSpan.Zero, synthesis.ReportInstance.ReportType, nextStep,
            RepositoryAnalyzed: repositoryContext.RepositoryName, FilesChanged: repositoryContext.ChangedPaths);
    }

    private ExecutionResult FromRepositoryValidation(EngineeringRequest request)
    {
        var repositoryContext = TryGetRepositoryContext(request);
        if (repositoryContext is null)
        {
            return MissingRepositoryContext(null, "AGT-EXE-0010");
        }

        var outcome = InvokeSkill<ValidationOutcome>("REPO-VALIDATION", "RepositoryContext", repositoryContext, request.ProjectProfileId, "RepositoryValidation");

        if (outcome is null)
        {
            return new ExecutionResult(false, null, "AGT-EXE-0010", new[] { "REPO-VALIDATION is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        if (outcome.IsEscalated)
        {
            return new ExecutionResult(false, null, "AGT-EXE-0010", new[] { outcome.EscalationReason! }, null, TimeSpan.Zero, null, null,
                RepositoryAnalyzed: repositoryContext.RepositoryName, FilesChanged: repositoryContext.ChangedPaths, TestsExecuted: false);
        }

        // IsEscalated only means "no verification procedure could be
        // constructed" - a real, completed run that failed (non-zero exit)
        // is still a Recorded outcome, never treated as a success (Story
        // S31's own "log it rather than silently omitting it" discipline).
        var passed = outcome.LogEntry!.Passed;
        var diagnostics = passed
            ? Array.Empty<string>()
            : new[] { $"Validation command exited with code {outcome.LogEntry.ExitCode}: {outcome.LogEntry.StandardErrorExcerpt}" };

        return new ExecutionResult(passed, null, "AGT-EXE-0010", diagnostics, null, TimeSpan.Zero, null, null,
            RepositoryAnalyzed: repositoryContext.RepositoryName, FilesChanged: repositoryContext.ChangedPaths, TestsExecuted: true);
    }

    private ExecutionResult FromEngineeringSpecificationReview(EngineeringRequest request)
    {
        if (request.Payload is null || !request.Payload.TryGetValue("SpecificationReviewRequest", out var raw)
            || raw is not DomainContent.EngineeringSpecificationReview.SpecificationReviewRequest reviewRequest)
        {
            return new ExecutionResult(false, "WFL-DEL-0007", "AGT-EXE-0013",
                new[] { "Payload must carry a SpecificationReviewRequest." }, null, TimeSpan.Zero, null, null);
        }

        var outcome = InvokeSkill<DomainContent.EngineeringSpecificationReview.SpecificationReviewOutcome>(
            "SKL-EXE-0026", "SpecificationReviewRequest", reviewRequest, request.ProjectProfileId, "SpecificationReview");

        if (outcome is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0007", "AGT-EXE-0013", new[] { "SKL-EXE-0026 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        if (outcome.IsEscalated)
        {
            return new ExecutionResult(false, "WFL-DEL-0007", "AGT-EXE-0013", new[] { outcome.EscalationReason! }, null, TimeSpan.Zero, null, null);
        }

        var findingsSummary = outcome.ReviewInstance!.Findings.Select(f => $"[{f.Severity}] {f.Area}: {f.Reason}").ToList();
        return new ExecutionResult(true, "WFL-DEL-0007", "AGT-EXE-0013", findingsSummary, null, TimeSpan.Zero,
            outcome.FinalRecommendation, "Independent review complete.");
    }

    private ExecutionResult FromImplementationValidation(EngineeringRequest request)
    {
        if (request.Payload is null
            || !request.Payload.TryGetValue("SpecificationConformanceValidationRequest", out var confRaw)
            || confRaw is not DomainContent.ImplementationValidation.SpecificationConformanceValidationRequest confRequest
            || !request.Payload.TryGetValue("ArchitectureStandardsConformanceReviewRequest", out var archRaw)
            || archRaw is not DomainContent.ImplementationValidation.ArchitectureStandardsConformanceReviewRequest archRequest)
        {
            return new ExecutionResult(false, "WFL-DEL-0005", "AGT-EXE-0014",
                new[] { "Payload must carry SpecificationConformanceValidationRequest and ArchitectureStandardsConformanceReviewRequest." }, null, TimeSpan.Zero, null, null);
        }

        var conformance = InvokeSkill<DomainContent.ImplementationValidation.SpecificationConformanceValidationResult>(
            "SKL-EXE-0027", "SpecificationConformanceValidationRequest", confRequest, request.ProjectProfileId, "Validation-Conformance");
        if (conformance is null || conformance.IsEscalated)
        {
            return new ExecutionResult(false, "WFL-DEL-0005", "AGT-EXE-0014",
                new[] { conformance?.EscalationReason ?? "SKL-EXE-0027 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var architecture = InvokeSkill<DomainContent.ImplementationValidation.ArchitectureStandardsConformanceReviewResult>(
            "SKL-EXE-0028", "ArchitectureStandardsConformanceReviewRequest", archRequest, request.ProjectProfileId, "Validation-Architecture");
        if (architecture is null || architecture.IsEscalated)
        {
            return new ExecutionResult(false, "WFL-DEL-0005", "AGT-EXE-0014",
                new[] { architecture?.EscalationReason ?? "SKL-EXE-0028 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var combinedFindings = conformance.ConformanceFindings!.Concat(architecture.ConformanceFindings!).ToList();
        var synthesis = InvokeSkill<DomainContent.ImplementationValidation.ValidationFindingsSynthesisResult>(
            "SKL-EXE-0029", "ValidationFindingsSynthesisRequest",
            new DomainContent.ImplementationValidation.ValidationFindingsSynthesisRequest(combinedFindings),
            request.ProjectProfileId, "Validation-Synthesis");

        if (synthesis is null || synthesis.IsEscalated)
        {
            return new ExecutionResult(false, "WFL-DEL-0005", "AGT-EXE-0014",
                new[] { synthesis?.EscalationReason ?? "SKL-EXE-0029 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var diagnosticsSummary = conformance.UncoveredSpecificationSections!
            .Select(s => $"Uncovered specification section: {s}")
            .Concat(synthesis.ReviewInstance!.Findings.Select(f => $"[{f.Severity}] {f.Area}: {f.Reason}"))
            .ToList();

        var isReady = synthesis.MergeReadinessSignal == "ready";
        return new ExecutionResult(isReady, "WFL-DEL-0005", "AGT-EXE-0014", diagnosticsSummary, null, TimeSpan.Zero,
            synthesis.MergeReadinessSignal,
            isReady ? "No further action needed." : "Awaiting Human Approval at the Review gate.");
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
