using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.ImplementationValidation;

public sealed class SpecificationConformanceValidationBehavior : IBehavior
{
    private const string PromptContractId = "PCT-EXE-0027";
    private readonly AiExecutionService _aiExecutionService;
    private readonly IPromptCatalog _promptCatalog;

    public SpecificationConformanceValidationBehavior(AiExecutionService aiExecutionService, IPromptCatalog promptCatalog)
    {
        _aiExecutionService = aiExecutionService;
        _promptCatalog = promptCatalog;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("SpecificationConformanceValidationRequest", out var raw) || raw is not SpecificationConformanceValidationRequest request)
        {
            return SpecificationConformanceValidationResult.Escalated("No SpecificationConformanceValidationRequest was supplied in the execution context.");
        }

        var template = _promptCatalog.Get(PromptContractId)!;
        var renderResult = PromptRenderer.Render(template, new Dictionary<string, object?>
        {
            ["specification_instance"] = request.SpecificationInstance,
            ["implementation_log"] = request.ImplementationLog,
            ["test_plan_and_results"] = request.TestPlanAndResults,
        });

        if (!renderResult.Success)
        {
            return SpecificationConformanceValidationResult.Escalated($"Prompt rendering failed, missing: {string.Join(", ", renderResult.MissingVariables)}.");
        }

        var executionOutcome = _aiExecutionService.Execute(new AiPromptRequest(PromptContractId, template.SystemPrompt, renderResult.RenderedUserPrompt!), CancellationToken.None);
        if (!executionOutcome.Success)
        {
            return SpecificationConformanceValidationResult.Escalated(executionOutcome.FailureReason!);
        }

        if (!StructuredResponseParser.TryParse<AiConformanceValidationResponse>(executionOutcome.Result!.RawText, out var parsed, out var errors))
        {
            return SpecificationConformanceValidationResult.Escalated($"AI response for {PromptContractId} could not be parsed: {string.Join("; ", errors)}.");
        }

        var findings = parsed!.Conformance_Findings.Select(f => new ConformanceFinding(f.Area, f.Classification, f.Rationale)).ToList();
        return SpecificationConformanceValidationResult.Completed(findings, parsed.Uncovered_Specification_Sections ?? new List<string>());
    }
}
