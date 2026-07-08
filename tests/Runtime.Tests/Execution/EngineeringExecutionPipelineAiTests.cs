using Kernel.Registry;
using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Bootstrapping;
using Runtime.DomainContent.ConversationManagement;
using Runtime.DomainContent.EngineeringSpecificationReview;
using Runtime.DomainContent.ImplementationValidation;
using Runtime.Execution;
using Runtime.Hosts.Skill;
using Xunit;

namespace Runtime.Tests.Execution;

public class EngineeringExecutionPipelineAiTests
{
    private sealed class StubProvider : IAiProvider
    {
        private readonly string _rawText;
        public StubProvider(string rawText) => _rawText = rawText;
        public string Name => "Stub";
        public Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCompletionResult(_rawText, new AiUsage(1, 1, 2), "Stub", "test-model"));
    }

    private static AiExecutionService ServiceReturning(string rawText) =>
        new(new StubProvider(rawText), timeout: TimeSpan.FromSeconds(5), delay: _ => { });

    private static RuntimeHandle BootWithBehaviors(IReadOnlyDictionary<string, IBehavior> behaviors) =>
        RuntimeBootstrapper.Boot(new RuntimeConfiguration(PolicyResolver: new AllowAllPolicyResolver()), behaviors);

    private static IReadOnlyDictionary<string, IBehavior> ConversationBehaviors(IReadOnlyDictionary<string, IBehavior> extra)
    {
        var behaviors = new Dictionary<string, IBehavior>
        {
            ["SKL-EXE-0021"] = new ConversationIntakeAndRoutingBehavior(),
            ["SKL-EXE-0022"] = new ConversationHandoffAndFollowupBehavior(),
        };
        foreach (var (id, behavior) in extra)
        {
            behaviors[id] = behavior;
        }
        return behaviors;
    }

    [Fact]
    public void RoutesReviewSpecificationRequestToWfl0007AndReturnsFinalRecommendation()
    {
        var raw = """
        {"review_instance":{"review_target":"SPEC-1","review_iteration":1,"findings":[]},"final_recommendation":"Approved","status":"review_complete","failure_reason":null}
        """;
        var behaviors = ConversationBehaviors(new Dictionary<string, IBehavior>
        {
            ["SKL-EXE-0026"] = new SpecificationIndependentReviewBehavior(ServiceReturning(raw), new EngineeringPromptCatalog()),
        });
        var runtime = BootWithBehaviors(behaviors);
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0026", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);
        var payload = new Dictionary<string, object>
        {
            ["SpecificationReviewRequest"] = new SpecificationReviewRequest("SPEC-1 content", 1),
        };

        var result = pipeline.Execute(new EngineeringRequest("please review specification for SPEC-1", "default-profile", payload));

        Assert.True(result.Success);
        Assert.Equal("WFL-DEL-0007", result.ExecutedWorkflowId);
        Assert.Equal("AGT-EXE-0013", result.ExecutedAgentId);
        Assert.Equal("Completed via WFL-DEL-0007.", result.PresentedResult);
    }

    [Fact]
    public void RoutesMergeReadinessRequestToWfl0005AndChainsAllThreeValidationSkills()
    {
        var conformanceRaw = """{"conformance_findings":[{"area":"Tests","classification":"PASS","rationale":"covered"}],"uncovered_specification_sections":[]}""";
        var architectureRaw = """{"conformance_findings":[{"area":"Security","classification":"PASS","rationale":"no findings"}]}""";
        var synthesisRaw = """
        {"review_instance":{"review_target":"Implementation Validation","review_iteration":1,"findings":[]},"merge_readiness_signal":"ready"}
        """;
        var behaviors = ConversationBehaviors(new Dictionary<string, IBehavior>
        {
            ["SKL-EXE-0027"] = new SpecificationConformanceValidationBehavior(ServiceReturning(conformanceRaw), new EngineeringPromptCatalog()),
            ["SKL-EXE-0028"] = new ArchitectureStandardsConformanceReviewBehavior(ServiceReturning(architectureRaw), new EngineeringPromptCatalog()),
            ["SKL-EXE-0029"] = new ValidationFindingsSynthesisBehavior(ServiceReturning(synthesisRaw), new EngineeringPromptCatalog()),
        });
        var runtime = BootWithBehaviors(behaviors);
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0027", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0028", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0029", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);
        var payload = new Dictionary<string, object>
        {
            ["SpecificationConformanceValidationRequest"] = new SpecificationConformanceValidationRequest(null, "log", "results"),
            ["ArchitectureStandardsConformanceReviewRequest"] = new ArchitectureStandardsConformanceReviewRequest("profile", "docs", null),
        };

        var result = pipeline.Execute(new EngineeringRequest("check merge readiness for this implementation", "default-profile", payload));

        Assert.True(result.Success);
        Assert.Equal("WFL-DEL-0005", result.ExecutedWorkflowId);
        Assert.Equal("AGT-EXE-0014", result.ExecutedAgentId);
        Assert.Equal("Completed via WFL-DEL-0005.", result.PresentedResult);
    }

    private sealed class AllowAllPolicyResolver : Kernel.Governance.IPolicyResolver
    {
        public Kernel.Governance.PolicyResolution Resolve(Kernel.Governance.ActivationRequest request) => Kernel.Governance.PolicyResolution.Authorize();
    }
}
