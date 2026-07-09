using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.RepositoryMaintenance;

// The executable realization of SKL-EXE-0042. Each of the design's eight
// Health dimensions is meant to resolve via Technology-Profile-resolved
// Automation - confirmed absent from this Runtime (no adapters/ directory
// has ever been materialized, per the design's own honest gap
// acknowledgment: 2026-07-08-engineering-maintenance-and-repository-
// hygiene-design.md, "A genuinely unanticipated capability"). Every
// dimension therefore classifies UNKNOWN with a recorded rationale,
// reusing SKL-EXE-0017's exception-recording discipline verbatim
// (Responsibility 1's own postcondition) - never a fabricated
// PASS/WARNING/FAIL guess.
public sealed class RepositoryHealthAssessmentBehavior : IBehavior
{
    private static readonly string[] Dimensions =
    {
        "Dependency health", "SDK health", "Package health", "Configuration health",
        "Project structure health", "Build health", "Documentation health", "Code hygiene",
    };

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("RepositoryHealthAssessmentRequest", out var raw) || raw is not RepositoryHealthAssessmentRequest request)
        {
            return new RepositoryHealthAssessmentResult(Array.Empty<HealthDimensionFinding>(), new[] { "No repository health assessment request was supplied." });
        }

        var stackDescription = request.RepositoryContext.TechnologyProfile is { } profile
            ? $"Current stack, per Repository Intelligence: {string.Join("; ", profile.Technologies.Select(t => t.Name))}."
            : "No Technology Profile is available.";

        var findings = Dimensions
            .Select(dimension => new HealthDimensionFinding(
                dimension,
                "UNKNOWN",
                $"No Technology-Profile-resolved Automation exists for '{dimension}' in this Runtime - requires human input. {stackDescription}"))
            .ToList();

        return new RepositoryHealthAssessmentResult(findings, new[] { "Every Health dimension requires human input - no per-stack Automation is materialized in this Runtime yet." });
    }
}
