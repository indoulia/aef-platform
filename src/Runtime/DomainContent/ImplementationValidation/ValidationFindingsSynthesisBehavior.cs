using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Context;
using Runtime.DomainContent.EngineeringSpecificationReview;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.ImplementationValidation;

public sealed class ValidationFindingsSynthesisBehavior : IBehavior
{
    private const string PromptContractId = "PCT-EXE-0029";
    private readonly AiExecutionService _aiExecutionService;
    private readonly IPromptCatalog _promptCatalog;

    public ValidationFindingsSynthesisBehavior(AiExecutionService aiExecutionService, IPromptCatalog promptCatalog)
    {
        _aiExecutionService = aiExecutionService;
        _promptCatalog = promptCatalog;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("ValidationFindingsSynthesisRequest", out var raw) || raw is not ValidationFindingsSynthesisRequest request)
        {
            return ValidationFindingsSynthesisResult.Escalated("No ValidationFindingsSynthesisRequest was supplied in the execution context.");
        }

        var template = _promptCatalog.Get(PromptContractId)!;
        var renderResult = PromptRenderer.Render(template, new Dictionary<string, object?>
        {
            ["conformance_findings"] = request.ConformanceFindings,
        });

        if (!renderResult.Success)
        {
            return ValidationFindingsSynthesisResult.Escalated($"Prompt rendering failed, missing: {string.Join(", ", renderResult.MissingVariables)}.");
        }

        var executionOutcome = _aiExecutionService.Execute(new AiPromptRequest(PromptContractId, template.SystemPrompt, renderResult.RenderedUserPrompt!), CancellationToken.None);
        if (!executionOutcome.Success)
        {
            return ValidationFindingsSynthesisResult.Escalated(executionOutcome.FailureReason!);
        }

        if (!StructuredResponseParser.TryParse<AiSynthesisResponse>(executionOutcome.Result!.RawText, out var parsed, out var errors))
        {
            return ValidationFindingsSynthesisResult.Escalated($"AI response for {PromptContractId} could not be parsed: {string.Join("; ", errors)}.");
        }

        if (parsed!.Review_Instance is null || parsed.Review_Instance.Findings is null || parsed.Merge_Readiness_Signal is null)
        {
            return ValidationFindingsSynthesisResult.Escalated($"AI response for {PromptContractId} was missing review_instance or merge_readiness_signal.");
        }

        var reviewInstance = new EngineeringReviewInstance(
            parsed.Review_Instance.Review_Target,
            ReviewIteration: 1,
            parsed.Review_Instance.Findings.Select(f => new EngineeringReviewFinding(f.Area, f.Severity, f.Reason, f.Evidence, f.Recommendation)).ToList());

        return ValidationFindingsSynthesisResult.Completed(reviewInstance, parsed.Merge_Readiness_Signal);
    }
}
