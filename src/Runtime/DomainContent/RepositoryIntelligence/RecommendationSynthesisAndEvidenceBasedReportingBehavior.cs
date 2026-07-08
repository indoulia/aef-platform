using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.RepositoryIntelligence;

// The executable realization of SKL-EXE-0033. requires_human_approval is
// set per PCT-EXE-0033's own postcondition: true whenever any finding is
// a Root Cause Conclusion (Diagnostics ran), an Architecture
// Recommendation, or a Risk Classification (approximated here as any FAIL
// finding, the only class this Runtime's deterministic Behaviors can
// produce that plausibly warrants one) - never silently bypassed.
public sealed class RecommendationSynthesisAndEvidenceBasedReportingBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("CombinedIntelligenceFindings", out var raw) || raw is not CombinedIntelligenceFindings combined)
        {
            var emptyReport = new RecommendationReport("Understanding", "unknown", Array.Empty<IntelligenceFinding>(), new[] { "No combined findings were supplied." });
            return new RecommendationSynthesisResult(emptyReport, false, null);
        }

        var findings = new List<IntelligenceFinding>();
        findings.AddRange(combined.Understanding?.Findings ?? Array.Empty<IntelligenceFinding>());
        findings.AddRange(combined.CodeIntelligence?.Findings ?? Array.Empty<IntelligenceFinding>());

        var diagnosticsRan = combined.Diagnostics is not null;
        var hasFailFinding = findings.Any(f => f.Class == FindingClass.Fail);
        var requiresApproval = diagnosticsRan || hasFailFinding;

        var reportType = combined.Diagnostics is not null ? "Root Cause Analysis" : "Understanding";
        var provenanceNotes = new List<string>
        {
            $"Analysis Date: {DateTime.UtcNow:O}",
            "Analysis Tooling: aef-platform deterministic Repository Intelligence Behaviors (no LLM integration).",
        };

        var report = new RecommendationReport(reportType, "request-scoped (see Findings evidence)", findings, provenanceNotes);

        PendingReviewInstance? reviewInstance = requiresApproval
            ? new PendingReviewInstance(diagnosticsRan ? "Root Cause Conclusion" : "Risk Classification", $"{findings.Count} finding(s) require Human Approval before this report is treated as final.")
            : null;

        return new RecommendationSynthesisResult(report, requiresApproval, reviewInstance);
    }
}
