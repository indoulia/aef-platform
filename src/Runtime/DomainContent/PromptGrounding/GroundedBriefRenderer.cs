using System.Text;

namespace Runtime.DomainContent.PromptGrounding;

// The brief as the calling agent reads it. The agent pays for every character on every call, so this prints only
// what adds information: no restated goal, no empty sections, no second copy of the same lists.
public static class GroundedBriefRenderer
{
    public const int MaxBriefCharacters = 6000;
    public const int MaxCandidateFiles = 5;
    public const string NothingToAdd = "No repository facts to add for this request.";
    public const string ClosingLine = "Verified facts are checked against the repository. Ask the user the open questions before editing.";

    public static string Render(GroundedBrief brief)
    {
        var strong = new HashSet<string>(brief.StrongFiles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var files = brief.WhereToWork.Select(reference => reference.File).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var confirmed = files.Where(strong.Contains).ToList();
        var candidates = files.Where(file => !strong.Contains(file)).ToList();

        var where = new List<string>();
        if (confirmed.Count > 0)
        {
            where.Add($"Work in: {string.Join(", ", confirmed)}.");
        }

        if (candidates.Count > 0)
        {
            var hidden = candidates.Count - Math.Min(candidates.Count, MaxCandidateFiles) + brief.OmittedCount;
            where.Add($"Candidate files (confirm which apply before editing): {string.Join(", ", candidates.Take(MaxCandidateFiles))}." + (hidden > 0 ? $" (+{hidden} more)" : string.Empty));
        }

        if (brief.TestFiles.Count > 0)
        {
            where.Add($"Covering tests: {string.Join(", ", brief.TestFiles)}.");
        }

        var builder = new StringBuilder();
        var hasSubstance = brief.VerifiedFacts.Count > 0 || brief.OpenQuestions.Count > 0 || where.Count > 0 || brief.Assumptions.Count > 0;
        if (!hasSubstance)
        {
            builder.Append(NothingToAdd).Append('\n');
            if (brief.BuildAndTestCommand is not null)
            {
                builder.Append("Build and test: `").Append(brief.BuildAndTestCommand).Append("` (conventional for the stack, unverified).");
            }

            return builder.ToString().TrimEnd();
        }

        Section(builder, "Verified facts", brief.VerifiedFacts.Select(fact => $"{fact.Statement} ({(fact.Line is > 0 ? $"{fact.File}:{fact.Line}" : fact.File)})"));
        Section(builder, "Open questions", brief.OpenQuestions);
        Section(builder, "Where to work", where);
        Section(builder, "Build and test", brief.BuildAndTestCommand is null ? Array.Empty<string>() : new[] { $"`{brief.BuildAndTestCommand}` (conventional for the stack, unverified)" });
        Section(builder, "Assumptions", brief.Assumptions);
        Section(builder, "Done when", brief.AcceptanceChecks);

        if (brief.VerifiedFacts.Count > 0 || brief.OpenQuestions.Count > 0)
        {
            builder.Append(ClosingLine);
        }

        var text = builder.ToString().TrimEnd();
        return text.Length <= MaxBriefCharacters ? text : text[..(MaxBriefCharacters - 4)] + "\n...";
    }

    private static void Section(StringBuilder builder, string heading, IEnumerable<string> lines)
    {
        var items = lines.ToList();
        if (items.Count == 0)
        {
            return;
        }

        builder.Append("## ").Append(heading).Append('\n');
        foreach (var line in items)
        {
            builder.Append("- ").Append(line).Append('\n');
        }

        builder.Append('\n');
    }
}
