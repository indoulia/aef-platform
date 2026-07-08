using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.EngineeringSpecificationReview;

// Realizes SKL-EXE-0026 for AGT-EXE-0013 (PCT-EXE-0026). Per that
// Prompt Contract: status="terminal_failure" is reserved exclusively
// for structural failures preventing evaluation (schema non-conformance,
// unreadable input, an AI call that fails outright, or a response that
// cannot be parsed) - it is never used for an ordinary "Requires
// Revision"/"Rejected" outcome, which is carried in FinalRecommendation
// as an escalation-free, successful result.
public sealed class SpecificationIndependentReviewBehavior : IBehavior
{
    private const string PromptContractId = "PCT-EXE-0026";
    private readonly AiExecutionService _aiExecutionService;
    private readonly IPromptCatalog _promptCatalog;

    public SpecificationIndependentReviewBehavior(AiExecutionService aiExecutionService, IPromptCatalog promptCatalog)
    {
        _aiExecutionService = aiExecutionService;
        _promptCatalog = promptCatalog;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("SpecificationReviewRequest", out var raw) || raw is not SpecificationReviewRequest request)
        {
            return SpecificationReviewOutcome.Escalated("No SpecificationReviewRequest was supplied in the execution context.");
        }

        var template = _promptCatalog.Get(PromptContractId);
        if (template is null)
        {
            return SpecificationReviewOutcome.Escalated($"No prompt template registered for {PromptContractId}.");
        }

        var renderResult = PromptRenderer.Render(template, new Dictionary<string, object?>
        {
            ["specification_instance"] = request.SpecificationInstance,
            ["review_iteration"] = request.ReviewIteration,
        });

        if (!renderResult.Success)
        {
            return SpecificationReviewOutcome.Escalated($"Prompt rendering failed, missing: {string.Join(", ", renderResult.MissingVariables)}.");
        }

        var executionOutcome = _aiExecutionService.Execute(
            new AiPromptRequest(PromptContractId, template.SystemPrompt, renderResult.RenderedUserPrompt!),
            CancellationToken.None);

        if (!executionOutcome.Success)
        {
            return SpecificationReviewOutcome.Escalated(executionOutcome.FailureReason!);
        }

        if (!StructuredResponseParser.TryParse<AiSpecificationReviewResponse>(executionOutcome.Result!.RawText, out var parsed, out var errors))
        {
            return SpecificationReviewOutcome.Escalated($"AI response for {PromptContractId} could not be parsed: {string.Join("; ", errors)}.");
        }

        if (parsed!.Status == "terminal_failure")
        {
            return SpecificationReviewOutcome.Escalated(parsed.Failure_Reason ?? "AI reported a terminal_failure with no reason.");
        }

        if (parsed.Review_Instance is null || parsed.Final_Recommendation is null)
        {
            return SpecificationReviewOutcome.Escalated($"AI response for {PromptContractId} was missing review_instance or final_recommendation.");
        }

        var reviewInstance = new EngineeringReviewInstance(
            parsed.Review_Instance.Review_Target,
            parsed.Review_Instance.Review_Iteration,
            parsed.Review_Instance.Findings.Select(f => new EngineeringReviewFinding(f.Area, f.Severity, f.Reason, f.Evidence, f.Recommendation)).ToList());

        return SpecificationReviewOutcome.Completed(reviewInstance, parsed.Final_Recommendation);
    }
}
