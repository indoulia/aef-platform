using System.Text;
using System.Text.RegularExpressions;
using Runtime.Context;
using Runtime.Hosts.Skill;
using Runtime.Integration.CodeIntelligence;

namespace Runtime.DomainContent.ChangeCheck;

// Evidence about a change an agent has already made. Reads only. A check it cannot evaluate mechanically is
// reported "cannot tell" - never guessed met.
public sealed class ChangeCheckSkillBehavior : IBehavior
{
    public const string Met = "met";
    public const string NotMet = "not met";
    public const string CannotTell = "cannot tell";

    private const string ConfinedCheckPrefix = "Changes are confined to the files under Where to work";
    private static readonly Regex CommandCheck = new("^`.+` exits 0$", RegexOptions.Compiled);
    private static readonly Regex TestsCheck = new("^Tests in .+ pass$", RegexOptions.Compiled);

    private readonly ICodeIntelligenceProvider _provider;

    public ChangeCheckSkillBehavior(ICodeIntelligenceProvider provider)
    {
        _provider = provider;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("ChangeCheckRequest", out var raw) || raw is not ChangeCheckRequest request)
        {
            return null;
        }

        var notes = new List<string>();
        var changes = request.Changes;
        if (!changes.Succeeded)
        {
            notes.Add(changes.FailureReason ?? "The changed files could not be determined.");
        }
        else
        {
            notes.Add($"Compared against {changes.ResolvedBaseRef}.");
            if (changes.Files.Count == 0)
            {
                notes.Insert(0, $"No changed files were found since {changes.ResolvedBaseRef}.");
            }
        }

        var files = changes.Files;
        var expected = new HashSet<string>(request.ExpectedFiles.Select(Normalize), StringComparer.OrdinalIgnoreCase);
        var outside = expected.Count == 0 ? new List<string>() : files.Where(file => !expected.Contains(Normalize(file))).ToList();

        IReadOnlyList<CodeReference> affected = Array.Empty<CodeReference>();
        if (files.Count > 0)
        {
            var lookup = _provider.FindAffected(request.Repository.RepositoryRoot, files);
            if (lookup.Available)
            {
                affected = lookup.References;
            }
            else
            {
                notes.Add($"Affected tests could not be determined: {lookup.UnavailableReason}");
            }
        }

        bool? passed = null;
        string? detail = null;
        if (request.Validation is null)
        {
            if (files.Count > 0)
            {
                notes.Add("Build and tests were not run.");
            }
        }
        else if (request.Validation.IsEscalated)
        {
            detail = request.Validation.EscalationReason;
        }
        else
        {
            var entry = request.Validation.LogEntry!;
            passed = entry.Passed;
            detail = entry.Passed ? "Exit code 0." : $"Exit code {entry.ExitCode}: {Excerpt(entry.StandardErrorExcerpt, entry.StandardOutputExcerpt)}";
        }

        // The command validation actually ran, derived exactly as RepositoryValidationSkillBehavior derives it.
        var stackNames = request.Repository.TechnologyProfile?.Technologies.Select(technology => technology.Name).ToList() ?? new List<string>();
        var ranCommand = RepositoryValidation.BuildSystemDetector.DetectDefaultValidationCommand(stackNames) is { } command
            ? (command.Command + " " + string.Join(' ', command.Arguments)).Trim()
            : null;

        var checks = request.AcceptanceChecks
            .Where(check => !string.IsNullOrWhiteSpace(check))
            .Select(check => Evaluate(check.Trim(), files, expected.Count, outside, passed, detail, request.Validation is null, ranCommand))
            .ToList();

        return new ChangeCheckReport(files, outside, affected, passed, detail, checks, notes);
    }

    private static AcceptanceCheckResult Evaluate(string check, IReadOnlyList<string> files, int expectedCount, IReadOnlyList<string> outside, bool? passed, string? detail, bool validationNotRun, string? ranCommand)
    {
        if (files.Count == 0)
        {
            return new AcceptanceCheckResult(check, CannotTell, "No changes were found to check.");
        }

        if (check.StartsWith(ConfinedCheckPrefix, StringComparison.Ordinal))
        {
            if (expectedCount == 0)
            {
                return new AcceptanceCheckResult(check, CannotTell, "No ExpectedFiles were supplied.");
            }

            return outside.Count == 0
                ? new AcceptanceCheckResult(check, Met, "Every changed file is in the expected set.")
                : new AcceptanceCheckResult(check, NotMet, $"Changed outside the expected set: {string.Join(", ", outside)}. Explain or revert each.");
        }

        if (CommandCheck.IsMatch(check))
        {
            // Only the command that was actually run can be reported on; another command's result is unknown.
            var asked = check[1..check.LastIndexOf('`')].Trim();
            if (!string.Equals(asked, ranCommand, StringComparison.OrdinalIgnoreCase))
            {
                return new AcceptanceCheckResult(check, CannotTell, ranCommand is null
                    ? "No build or test command is known for this repository, so this command was not run."
                    : $"This command was not run; the command run was `{ranCommand}`.");
            }
        }

        if (CommandCheck.IsMatch(check) || TestsCheck.IsMatch(check))
        {
            return passed switch
            {
                true => new AcceptanceCheckResult(check, Met, "The build and test command exited 0."),
                false => new AcceptanceCheckResult(check, NotMet, detail ?? "The build and test command failed."),
                _ => new AcceptanceCheckResult(check, CannotTell, validationNotRun ? "Build and tests were not run." : detail ?? "The build and test command could not be run."),
            };
        }

        return new AcceptanceCheckResult(check, CannotTell, "Not machine-checkable; needs human or agent review.");
    }

    private static string Normalize(string path) => path.Trim().Replace('\\', '/').TrimStart('/');

    private static string Excerpt(string standardError, string standardOutput)
    {
        var text = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
        text = text.Trim();
        return text.Length <= 600 ? text : text[^600..];
    }
}

public static class ChangeCheckRenderer
{
    public static string Render(ChangeCheckReport report)
    {
        var builder = new StringBuilder();
        Section(builder, "Files changed", report.FilesChanged);
        Section(builder, "Outside the expected files", report.FilesOutsideExpected);
        Section(builder, "Affected tests", report.Affected.Select(reference => reference.File));
        Section(builder, "Build and tests", new[]
        {
            report.BuildAndTestsPassed switch { true => "Passed.", false => "FAILED.", _ => "Not determined." } + (report.BuildAndTestDetail is null ? string.Empty : " " + report.BuildAndTestDetail),
        });
        Section(builder, "Acceptance checks", report.Checks.Select(check => $"[{check.Status}] {check.Check} - {check.Evidence}"));
        Section(builder, "Notes", report.Notes);
        return builder.ToString().TrimEnd();
    }

    private static void Section(StringBuilder builder, string heading, IEnumerable<string> lines)
    {
        builder.Append("## ").Append(heading).Append('\n');
        var any = false;
        foreach (var line in lines)
        {
            any = true;
            builder.Append("- ").Append(line).Append('\n');
        }

        if (!any)
        {
            builder.Append("- None.\n");
        }

        builder.Append('\n');
    }
}
