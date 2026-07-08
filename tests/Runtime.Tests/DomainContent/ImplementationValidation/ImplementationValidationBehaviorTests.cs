using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Context;
using Runtime.DomainContent.ImplementationValidation;
using Xunit;

namespace Runtime.Tests.DomainContent.ImplementationValidation;

public class ImplementationValidationBehaviorTests
{
    private sealed class StubProvider : IAiProvider
    {
        private readonly string _rawText;
        public StubProvider(string rawText) => _rawText = rawText;
        public string Name => "Stub";
        public Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCompletionResult(_rawText, new AiUsage(1, 1, 2), "Stub", "test-model"));
    }

    private static RuntimeExecutionContext ContextWith(string key, object request) =>
        new("test-instance", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Validation",
            new Dictionary<string, object> { [key] = request });

    [Fact]
    public void ConformanceValidationParsesFindingsAndUncoveredSections()
    {
        var raw = """
        {"conformance_findings":[{"area":"Tests","classification":"PASS","rationale":"covered"}],"uncovered_specification_sections":["NFR-3"]}
        """;
        var service = new AiExecutionService(new StubProvider(raw), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new SpecificationConformanceValidationBehavior(service, new EngineeringPromptCatalog());

        var result = behavior.Execute(ContextWith("SpecificationConformanceValidationRequest",
            new SpecificationConformanceValidationRequest(null, "log", "results"))) as SpecificationConformanceValidationResult;

        Assert.NotNull(result);
        Assert.False(result!.IsEscalated);
        Assert.Single(result.ConformanceFindings!);
        Assert.Equal(new[] { "NFR-3" }, result.UncoveredSpecificationSections);
    }

    [Fact]
    public void ArchitectureStandardsReviewParsesFindings()
    {
        var raw = """{"conformance_findings":[{"area":"Security","classification":"WARNING","rationale":"no rate limiting"}]}""";
        var service = new AiExecutionService(new StubProvider(raw), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new ArchitectureStandardsConformanceReviewBehavior(service, new EngineeringPromptCatalog());

        var result = behavior.Execute(ContextWith("ArchitectureStandardsConformanceReviewRequest",
            new ArchitectureStandardsConformanceReviewRequest("profile", "docs", null))) as ArchitectureStandardsConformanceReviewResult;

        Assert.NotNull(result);
        Assert.False(result!.IsEscalated);
        Assert.Single(result.ConformanceFindings!);
    }

    [Fact]
    public void SynthesisReturnsNotReadyWhenACriticalFindingIsPresent()
    {
        var raw = """
        {"review_instance":{"review_target":"Implementation Validation","review_iteration":1,"findings":[{"area":"Security","severity":"Critical","reason":"secret in code","evidence":"file.cs:10","recommendation":"remove"}]},"merge_readiness_signal":"not_ready_human_approval_required"}
        """;
        var service = new AiExecutionService(new StubProvider(raw), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new ValidationFindingsSynthesisBehavior(service, new EngineeringPromptCatalog());
        var findings = new[] { new ConformanceFinding("Security", "FAIL", "secret in code") };

        var result = behavior.Execute(ContextWith("ValidationFindingsSynthesisRequest",
            new ValidationFindingsSynthesisRequest(findings))) as ValidationFindingsSynthesisResult;

        Assert.NotNull(result);
        Assert.False(result!.IsEscalated);
        Assert.Equal("not_ready_human_approval_required", result.MergeReadinessSignal);
        Assert.Single(result.ReviewInstance!.Findings);
    }

    [Fact]
    public void EscalatesOnUnparseableAiResponse()
    {
        var service = new AiExecutionService(new StubProvider("not json"), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new SpecificationConformanceValidationBehavior(service, new EngineeringPromptCatalog());

        var result = behavior.Execute(ContextWith("SpecificationConformanceValidationRequest",
            new SpecificationConformanceValidationRequest(null, "log", "results"))) as SpecificationConformanceValidationResult;

        Assert.NotNull(result);
        Assert.True(result!.IsEscalated);
    }

    // A model can return syntactically valid JSON that simply omits a
    // required field (a common LLM failure mode) - System.Text.Json
    // silently leaves a missing non-nullable record field as null rather
    // than throwing, so each behavior must guard for it explicitly and
    // escalate, not dereference it and crash the host.
    [Fact]
    public void EscalatesInsteadOfThrowingWhenConformanceFindingsFieldIsMissing()
    {
        var service = new AiExecutionService(new StubProvider("{}"), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new SpecificationConformanceValidationBehavior(service, new EngineeringPromptCatalog());

        var result = behavior.Execute(ContextWith("SpecificationConformanceValidationRequest",
            new SpecificationConformanceValidationRequest(null, "log", "results"))) as SpecificationConformanceValidationResult;

        Assert.NotNull(result);
        Assert.True(result!.IsEscalated);
        Assert.NotNull(result.EscalationReason);
    }

    [Fact]
    public void ArchitectureReviewEscalatesInsteadOfThrowingWhenConformanceFindingsFieldIsMissing()
    {
        var service = new AiExecutionService(new StubProvider("{}"), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new ArchitectureStandardsConformanceReviewBehavior(service, new EngineeringPromptCatalog());

        var result = behavior.Execute(ContextWith("ArchitectureStandardsConformanceReviewRequest",
            new ArchitectureStandardsConformanceReviewRequest("profile", "docs", null))) as ArchitectureStandardsConformanceReviewResult;

        Assert.NotNull(result);
        Assert.True(result!.IsEscalated);
        Assert.NotNull(result.EscalationReason);
    }

    [Fact]
    public void SynthesisEscalatesInsteadOfThrowingWhenReviewInstanceFieldIsMissing()
    {
        var service = new AiExecutionService(new StubProvider("""{"merge_readiness_signal":"ready"}"""), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new ValidationFindingsSynthesisBehavior(service, new EngineeringPromptCatalog());
        var findings = new[] { new ConformanceFinding("Security", "PASS", "no findings") };

        var result = behavior.Execute(ContextWith("ValidationFindingsSynthesisRequest",
            new ValidationFindingsSynthesisRequest(findings))) as ValidationFindingsSynthesisResult;

        Assert.NotNull(result);
        Assert.True(result!.IsEscalated);
        Assert.NotNull(result.EscalationReason);
    }
}
