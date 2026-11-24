using Runtime.Context;
using Runtime.DomainContent.RepositoryValidation;
using Runtime.Hosts.Skill;
using Runtime.Integration.CodeIntelligence;

namespace Runtime.DomainContent.PromptGrounding;

// Turns a loose prompt into a brief a coding agent can act on without guessing. Deterministic and read-only:
// every Verified fact carries the file that proves it; anything unproven is an Assumption or an Open question.
public sealed class PromptGroundingSkillBehavior : IBehavior
{
    public const int MaxWhereToWork = 12;
    private const int MaxTestFiles = 8;
    private const int MaxIdentifiers = 15;
    private const int MaxFileSearchEntries = 20000;

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", "dist", "build", ".venv", "venv", "target", ".tokensave", ".next", "__pycache__",
    };

    private readonly ICodeIntelligenceProvider _provider;

    public PromptGroundingSkillBehavior(ICodeIntelligenceProvider provider)
    {
        _provider = provider;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("PromptGroundingRequest", out var raw) || raw is not PromptGroundingRequest request)
        {
            return null;
        }

        var root = request.Repository.RepositoryRoot;
        var facts = new List<EvidencedFact>();
        var assumptions = new List<string>();
        var questions = new List<string>();
        var named = new List<CodeReference>();
        string? unavailableReason = null;

        var identifiers = PromptIdentifierExtractor.Extract(request.UserPrompt).Take(MaxIdentifiers).ToList();
        foreach (var identifier in identifiers)
        {
            if (PromptIdentifierExtractor.IsPath(identifier) || PromptIdentifierExtractor.IsFileName(identifier))
            {
                var located = LocateFile(root, identifier);
                if (located is null)
                {
                    questions.Add(NotFound(identifier, Array.Empty<CodeReference>()));
                }
                else
                {
                    facts.Add(new EvidencedFact($"`{identifier}` exists", located, null, "filesystem"));
                    named.Add(new CodeReference(Path.GetFileName(located), "file", located, 0, null, "filesystem"));
                }

                continue;
            }

            var lookup = LookUpSymbol(root, identifier);
            if (!lookup.Available)
            {
                unavailableReason ??= lookup.UnavailableReason;
                assumptions.Add($"`{identifier}` is named in the prompt but could not be checked against the repository.");
                continue;
            }

            var exact = lookup.References.Where(reference => NameMatches(reference.Name, identifier)).ToList();
            if (exact.Count == 0)
            {
                questions.Add(NotFound(identifier, lookup.References));
                continue;
            }

            foreach (var reference in exact.Take(3))
            {
                facts.Add(new EvidencedFact($"`{reference.Name}` is a {reference.Kind}", reference.File, reference.Line, reference.Source));
                named.Add(reference);
            }
        }

        var relevant = _provider.FindRelevant(root, request.UserPrompt, MaxWhereToWork * 2);
        if (!relevant.Available)
        {
            unavailableReason ??= relevant.UnavailableReason;
        }

        var candidates = named.Concat(relevant.References)
            .GroupBy(reference => (reference.File.ToLowerInvariant(), reference.Line))
            .Select(group => group.First())
            .ToList();
        var whereToWork = candidates.Take(MaxWhereToWork).ToList();

        var testFiles = new List<string>();
        foreach (var file in whereToWork.Select(reference => reference.File).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var tests = _provider.FindTests(root, file);
            if (tests.Available)
            {
                testFiles.AddRange(tests.References.Select(reference => reference.File));
            }
        }

        testFiles = testFiles.Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxTestFiles).ToList();

        var technologies = request.Repository.TechnologyProfile?.Technologies ?? Array.Empty<Integration.RepositoryScanning.TechnologyEvidence>();
        foreach (var technology in technologies)
        {
            var evidenceFile = EvidenceFile(root, technology.Basis);
            if (evidenceFile is not null)
            {
                facts.Add(new EvidencedFact($"Detected stack: {technology.Name}", evidenceFile, null, "stack-detector"));
            }
            else
            {
                assumptions.Add($"Detected stack: {technology.Name} (basis: {technology.Basis}); no single evidencing file was identified.");
            }
        }

        var command = BuildSystemDetector.DetectDefaultValidationCommand(technologies.Select(technology => technology.Name).ToList());
        var commandText = command is null ? null : (command.Value.Command + " " + string.Join(' ', command.Value.Arguments)).Trim();
        if (commandText is null)
        {
            assumptions.Add("No build or test command could be detected for this repository.");
        }
        else
        {
            assumptions.Add($"`{commandText}` is the conventional command for the detected stack; the repository may define its own.");
        }

        if (unavailableReason is not null)
        {
            assumptions.Insert(0, $"Code intelligence unavailable: {unavailableReason.TrimEnd('.')}. File and symbol locations below were not verified.");
        }

        if (identifiers.Count == 0 && relevant.References.Count == 0)
        {
            questions.Add("The prompt does not name a file, symbol or feature that could be located. What part of the repository is this about?");
        }

        var checks = new List<string>();
        if (commandText is not null)
        {
            checks.Add($"`{commandText}` exits 0");
        }

        checks.Add("Changes are confined to the files under Where to work, or each extra file is explained");
        checks.Add("Every open question above is answered before editing");
        checks.AddRange(testFiles.Select(test => $"Tests in {test} pass"));

        return new GroundedBrief(
            Goal(request.UserPrompt),
            facts,
            whereToWork,
            testFiles,
            commandText,
            assumptions,
            questions,
            checks,
            candidates.Count - whereToWork.Count);
    }

    private CodeLookupResult LookUpSymbol(string root, string identifier)
    {
        // "OrderService.Submit" is indexed under its member and its type, not the dotted form.
        var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var names = parts.Length > 1 ? new[] { parts[^1], parts[0] } : new[] { identifier };

        var all = new List<CodeReference>();
        foreach (var name in names)
        {
            var result = _provider.FindSymbol(root, name, 5);
            if (!result.Available)
            {
                return result;
            }

            all.AddRange(result.References);
        }

        return CodeLookupResult.Found(all);
    }

    private static bool NameMatches(string referenceName, string identifier) =>
        identifier.Split('.', StringSplitOptions.RemoveEmptyEntries).Any(part => string.Equals(part, referenceName, StringComparison.OrdinalIgnoreCase));

    private static string NotFound(string identifier, IReadOnlyList<CodeReference> closest)
    {
        if (closest.Count == 0)
        {
            return $"The prompt mentions `{identifier}`, which was not found in the repository. Which file or symbol did you mean?";
        }

        var suggestions = string.Join(", ", closest.Take(3).Select(reference => $"`{reference.Name}` ({reference.File}:{reference.Line})"));
        return $"The prompt mentions `{identifier}`, which was not found in the repository. Closest matches: {suggestions}. Which did you mean?";
    }

    private static string Goal(string prompt)
    {
        var collapsed = string.Join(' ', prompt.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= 300 ? collapsed : collapsed[..300] + "...";
    }

    private static string? EvidenceFile(string root, string basis)
    {
        if (string.IsNullOrWhiteSpace(basis) || basis.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return null;
        }

        try
        {
            return File.Exists(Path.Combine(root, basis)) ? basis.Replace('\\', '/') : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return null;
        }
    }

    // A path is checked where it points; a bare file name is searched for, bounded, skipping build and dependency folders.
    private static string? LocateFile(string root, string identifier)
    {
        var relative = identifier.Replace('\\', '/').TrimStart('/');
        if (relative.StartsWith("./", StringComparison.Ordinal))
        {
            relative = relative[2..];
        }

        try
        {
            var full = Path.GetFullPath(Path.Combine(root, relative));
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) && (File.Exists(full) || Directory.Exists(full)))
            {
                return relative;
            }

            if (relative.Contains('/'))
            {
                return null;
            }

            var visited = 0;
            var pending = new Queue<string>();
            pending.Enqueue(Path.GetFullPath(root));
            while (pending.Count > 0 && visited < MaxFileSearchEntries)
            {
                var directory = pending.Dequeue();
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    visited++;
                    if (string.Equals(Path.GetFileName(file), relative, StringComparison.OrdinalIgnoreCase))
                    {
                        return Path.GetRelativePath(root, file).Replace('\\', '/');
                    }
                }

                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    visited++;
                    if (!IgnoredDirectories.Contains(Path.GetFileName(child)))
                    {
                        pending.Enqueue(child);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }

        return null;
    }
}
