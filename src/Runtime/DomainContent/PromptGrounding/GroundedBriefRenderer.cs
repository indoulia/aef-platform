using System.Text;
using Runtime.Integration.CodeIntelligence;

namespace Runtime.DomainContent.PromptGrounding;

// The brief as the calling agent reads it. Capped so it costs less than reading the files would.
public static class GroundedBriefRenderer
{
    public const int MaxBriefCharacters = 6000;
    public const string ClosingLine = "Treat Verified facts as ground truth. Ask the user the Open questions before editing.";

    public static string Render(GroundedBrief brief)
    {
        var whereToWork = brief.WhereToWork.ToList();
        var testFiles = brief.TestFiles.ToList();
        var trimmed = 0;

        var text = Compose(brief, whereToWork, testFiles, brief.OmittedCount);
        while (text.Length > MaxBriefCharacters && (whereToWork.Count > 0 || testFiles.Count > 0))
        {
            // Drop enough to matter per pass; one-at-a-time re-rendering is quadratic for nothing.
            var list = whereToWork.Count > 0 ? (System.Collections.IList)whereToWork : testFiles;
            var drop = Math.Max(1, list.Count / 4);
            for (var i = 0; i < drop; i++)
            {
                list.RemoveAt(list.Count - 1);
            }

            trimmed += drop;
            text = Compose(brief, whereToWork, testFiles, brief.OmittedCount + trimmed);
        }

        return text.Length <= MaxBriefCharacters ? text : text[..(MaxBriefCharacters - ClosingLine.Length - 5)] + "\n...\n" + ClosingLine;
    }

    private static string Compose(GroundedBrief brief, IReadOnlyList<CodeReference> whereToWork, IReadOnlyList<string> testFiles, int omitted)
    {
        var builder = new StringBuilder();

        Section(builder, "Goal", new[] { brief.Goal }, bullet: false);
        Section(builder, "Verified facts", brief.VerifiedFacts.Select(fact => $"{fact.Statement} ({Location(fact.File, fact.Line)})"));

        var strong = new HashSet<string>(brief.StrongFiles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var work = whereToWork.Select(reference =>
                (reference.Line > 0 ? $"{Location(reference.File, reference.Line)} - {reference.Kind} `{reference.Name}`" : reference.File)
                + (strong.Contains(reference.File) ? string.Empty : " (possible match, unconfirmed)"))
            .Concat(testFiles.Select(test => $"{test} - covering tests"))
            .ToList();
        if (omitted > 0)
        {
            work.Add($"({omitted} more omitted)");
        }

        Section(builder, "Where to work", work);
        Section(builder, "How to build and test", brief.BuildAndTestCommand is null ? Array.Empty<string>() : new[] { $"`{brief.BuildAndTestCommand}`" });
        Section(builder, "Assumptions", brief.Assumptions);
        Section(builder, "Open questions", brief.OpenQuestions);
        Section(builder, "Acceptance checks", brief.AcceptanceChecks);

        builder.Append("## Refined prompt\n");
        builder.Append(brief.Goal).Append('\n');
        var files = whereToWork.Select(reference => reference.File).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var strongFiles = files.Where(strong.Contains).ToList();
        if (strongFiles.Count > 0)
        {
            builder.Append("Work in: ").Append(string.Join(", ", strongFiles)).Append(".\n");
        }
        else if (files.Count > 0)
        {
            builder.Append("Candidate files (confirm which apply before editing): ").Append(string.Join(", ", files.Take(5))).Append(".\n");
        }

        if (brief.BuildAndTestCommand is not null)
        {
            builder.Append("Verify with `").Append(brief.BuildAndTestCommand).Append("`.\n");
        }

        builder.Append("Done when: ").Append(string.Join("; ", brief.AcceptanceChecks)).Append(".\n\n");
        builder.Append(ClosingLine);
        return builder.ToString();
    }

    private static string Location(string file, int? line) => line is > 0 ? $"{file}:{line}" : file;

    private static void Section(StringBuilder builder, string heading, IEnumerable<string> lines, bool bullet = true)
    {
        builder.Append("## ").Append(heading).Append('\n');
        var any = false;
        foreach (var line in lines)
        {
            any = true;
            builder.Append(bullet ? "- " : string.Empty).Append(line).Append('\n');
        }

        if (!any)
        {
            builder.Append("- None.\n");
        }

        builder.Append('\n');
    }
}
