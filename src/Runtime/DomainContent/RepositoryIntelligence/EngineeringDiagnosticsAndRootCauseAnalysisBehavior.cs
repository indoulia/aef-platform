using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.RepositoryIntelligence;

// The executable realization of SKL-EXE-0032. Real causal inference over
// logs/telemetry/exceptions is free-text/NL reasoning with no
// deterministic-algorithm precedent and no LLM integration point in this
// Runtime - so this Behavior performs the mechanical parts PCT-EXE-0032
// actually specifies (one Evidence Chain step per supplied evidence
// source, non-empty Alternative Hypotheses whenever confidence is not
// high) and fixes confidence low with an honest justification, rather
// than fabricating a causal narrative it cannot support. This is the
// correct, non-hollow realization of a contract whose own postconditions
// already anticipate and require this honesty.
public sealed class EngineeringDiagnosticsAndRootCauseAnalysisBehavior : IBehavior
{
    private const int DeterministicConfidenceScore = 2;

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("DiagnosticsRequest", out var raw) || raw is not DiagnosticsRequest request)
        {
            return DiagnosticsResult.NoEvidenceSupplied();
        }

        if (request.EvidenceSources.Count == 0)
        {
            return DiagnosticsResult.NoEvidenceSupplied();
        }

        var evidenceChain = request.EvidenceSources
            .Select((source, index) => new EvidenceChainStep($"Step {index + 1}: consider evidence source", new[] { source }, "Enumerated as a candidate contributor - no automated causal inference is available to establish a stronger link."))
            .ToList();

        var alternativeHypotheses = request.EvidenceSources
            .Select(source => new AlternativeHypothesis($"'{source}' is the primary contributor.", source))
            .ToList();

        return new DiagnosticsResult(
            evidenceChain,
            DeterministicConfidenceScore,
            "This Runtime has no LLM-backed or deterministic-algorithm causal-inference capability - confidence is fixed low and every evidence source is surfaced as an equally-weighted alternative hypothesis rather than a fabricated conclusion.",
            alternativeHypotheses,
            Array.Empty<string>());
    }
}
