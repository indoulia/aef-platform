using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.ImplementationValidation;

public sealed class ArchitectureStandardsConformanceReviewBehavior : IBehavior
{
    private const string PromptContractId = "PCT-EXE-0028";
    private readonly AiExecutionService _aiExecutionService;
    private readonly IPromptCatalog _promptCatalog;

    public ArchitectureStandardsConformanceReviewBehavior(AiExecutionService aiExecutionService, IPromptCatalog promptCatalog)
    {
        _aiExecutionService = aiExecutionService;
        _promptCatalog = promptCatalog;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("ArchitectureStandardsConformanceReviewRequest", out var raw) || raw is not ArchitectureStandardsConformanceReviewRequest request)
        {
            return ArchitectureStandardsConformanceReviewResult.Escalated("No ArchitectureStandardsConformanceReviewRequest was supplied in the execution context.");
        }

        var template = _promptCatalog.Get(PromptContractId)!;
        var renderResult = PromptRenderer.Render(template, new Dictionary<string, object?>
        {
            ["technology_profile"] = request.TechnologyProfile,
            ["documentation"] = request.Documentation,
            ["specification_instance"] = request.SpecificationInstance,
        });

        if (!renderResult.Success)
        {
            return ArchitectureStandardsConformanceReviewResult.Escalated($"Prompt rendering failed, missing: {string.Join(", ", renderResult.MissingVariables)}.");
        }

        var executionOutcome = _aiExecutionService.Execute(new AiPromptRequest(PromptContractId, template.SystemPrompt, renderResult.RenderedUserPrompt!), CancellationToken.None);
        if (!executionOutcome.Success)
        {
            return ArchitectureStandardsConformanceReviewResult.Escalated(executionOutcome.FailureReason!);
        }

        if (!StructuredResponseParser.TryParse<AiArchitectureReviewResponse>(executionOutcome.Result!.RawText, out var parsed, out var errors))
        {
            return ArchitectureStandardsConformanceReviewResult.Escalated($"AI response for {PromptContractId} could not be parsed: {string.Join("; ", errors)}.");
        }

        if (parsed!.Conformance_Findings is null)
        {
            return ArchitectureStandardsConformanceReviewResult.Escalated($"AI response for {PromptContractId} was missing conformance_findings.");
        }

        var findings = parsed.Conformance_Findings.Select(f => new ConformanceFinding(f.Area, f.Classification, f.Rationale)).ToList();
        return ArchitectureStandardsConformanceReviewResult.Completed(findings);
    }
}
