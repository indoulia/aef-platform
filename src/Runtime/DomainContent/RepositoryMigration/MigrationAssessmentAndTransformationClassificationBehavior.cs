using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.RepositoryMigration;

// The executable realization of SKL-EXE-0034. Transformation Classification
// is a deterministic keyword classifier, the same house convention
// ConversationIntakeAndRoutingBehavior already established for a
// cheap, low-ambiguity decision: never guess a category from an
// ambiguous goal - ask for clarification instead (PCT-EXE-0034's own
// postcondition). Current State is read from the already-detected
// Technology Profile (AGT-EXE-0007's own output, consumed, never
// re-derived); Target State always requires human input - no amount of
// keyword matching can honestly derive a precise target from a goal
// statement alone.
public sealed class MigrationAssessmentAndTransformationClassificationBehavior : IBehavior
{
    private static readonly (string Keyword, string Classification)[] KnownClassifications =
    {
        ("upgrade", "Technology/Framework Upgrade"),
        ("update the framework", "Technology/Framework Upgrade"),
        ("migrate", "Framework Migration"),
        ("port to", "Framework Migration"),
        ("replace", "Library/API Replacement"),
        ("modernize", "Architecture Modernization"),
        ("architecture", "Architecture Modernization"),
        ("split", "Repository Restructuring"),
        ("restructure", "Repository Restructuring"),
        ("reorganize", "Repository Restructuring"),
        ("across repositories", "Cross-Repository Migration"),
        ("cross-repository", "Cross-Repository Migration"),
        ("multiple repositories", "Cross-Repository Migration"),
    };

    private const string SupportedCategories =
        "Technology/Framework Upgrade, Framework Migration, Library/API Replacement, Architecture Modernization, Repository Restructuring, Cross-Repository Migration";

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("TransformationClassificationRequest", out var raw) || raw is not TransformationClassificationRequest request)
        {
            return new TransformationClassificationResult(
                "Requires Clarification", "Not determined - no request supplied.", "Not determined - no request supplied.",
                new[] { "No transformation classification request was supplied." });
        }

        var openItems = new List<string>();
        var classification = Classify(request.Goal);
        if (classification is null)
        {
            openItems.Add($"Which transformation category applies? Supported: {SupportedCategories}.");
        }

        var currentState = request.RepositoryContext.TechnologyProfile is { } profile
            ? $"Current stack, per Repository Intelligence: {string.Join("; ", profile.Technologies.Select(t => t.Name))}."
            : "No Technology Profile is available - current state requires human input.";

        openItems.Add("Target State requires human input - automated classification cannot derive a precise target from a goal statement alone.");

        return new TransformationClassificationResult(
            classification ?? "Requires Clarification",
            currentState,
            "Not determined by automated classification - requires human input.",
            openItems);
    }

    private static string? Classify(string goal)
    {
        var lowered = goal.ToLowerInvariant();
        foreach (var (keyword, classification) in KnownClassifications)
        {
            if (lowered.Contains(keyword))
            {
                return classification;
            }
        }

        return null;
    }
}
