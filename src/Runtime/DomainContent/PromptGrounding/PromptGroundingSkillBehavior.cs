using Runtime.Context;
using Runtime.DomainContent.RepositoryValidation;
using Runtime.Hosts.Skill;
using Runtime.Integration.CodeIntelligence;

namespace Runtime.DomainContent.PromptGrounding;

// Turns a loose prompt into a brief a coding agent can act on without guessing. Deterministic and read-only:
// every Verified fact carries the file that proves it; anything unproven is an Assumption or an Open question.
// A location is "confirmed" only when the prompt names it and it resolves to exactly one place on disk.
public sealed class PromptGroundingSkillBehavior : IBehavior
{
    public const int MaxWhereToWork = 12;
    private const int MaxTestFiles = 3;
    private const int MaxIdentifiers = 15;
    private const int MaxFileSearchEntries = 20000;
    private const int MaxFileMatches = 5;

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", "dist", "build", ".venv", "venv", "target", ".tokensave", ".next", "__pycache__",
    };

    private readonly ICodeIntelligenceProvider _provider;

    public PromptGroundingSkillBehavior(ICodeIntelligenceProvider provider)
    {
        _provider = provider;
    }

    // Stops asking after the first Unavailable answer: one hung or missing tool must cost one timeout, not twenty.
    private sealed class Guarded
    {
        private readonly ICodeIntelligenceProvider _inner;

        public Guarded(ICodeIntelligenceProvider inner) => _inner = inner;

        public string? UnavailableReason { get; private set; }

        public CodeLookupResult Ask(Func<ICodeIntelligenceProvider, CodeLookupResult> call)
        {
            if (UnavailableReason is not null)
            {
                return CodeLookupResult.Unavailable(UnavailableReason);
            }

            var result = call(_inner);
            if (result.ToolUnavailable)
            {
                UnavailableReason = result.UnavailableReason ?? "no reason given";
            }

            return result;
        }
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("PromptGroundingRequest", out var raw) || raw is not PromptGroundingRequest request)
        {
            return null;
        }

        var root = request.Repository.RepositoryRoot;
        var provider = new Guarded(_provider);
        var facts = new List<EvidencedFact>();
        var assumptions = new List<string>();
        var questions = new List<string>();
        var named = new List<CodeReference>();

        var identifiers = PromptIdentifierExtractor.Extract(request.UserPrompt).Take(MaxIdentifiers).ToList();
        foreach (var identifier in identifiers)
        {
            if (PromptIdentifierExtractor.IsPath(identifier) || PromptIdentifierExtractor.IsFileName(identifier))
            {
                GroundFile(root, identifier, facts, assumptions, questions, named);
            }
            else
            {
                GroundSymbol(root, identifier, provider, facts, assumptions, questions, named);
            }
        }

        var relevant = provider.Ask(p => p.FindRelevant(root, request.UserPrompt, MaxWhereToWork * 2));

        // Rank by how many distinct prompt terms point at a file. One loose match is a lead, not a location.
        var matchedTerms = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var searchHits = new List<CodeReference>();
        void Credit(CodeReference reference, string term)
        {
            if (!matchedTerms.TryGetValue(reference.File, out var terms))
            {
                matchedTerms[reference.File] = terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            terms.Add(term);
        }

        foreach (var reference in relevant.References)
        {
            Credit(reference, "~context");
        }

        foreach (var term in PromptIdentifierExtractor.ExtractSearchTerms(request.UserPrompt))
        {
            foreach (var reference in provider.Ask(p => p.FindSymbol(root, term, 10)).References)
            {
                Credit(reference, term);
                searchHits.Add(reference);
            }
        }

        var namedFiles = new HashSet<string>(named.Select(reference => reference.File), StringComparer.OrdinalIgnoreCase);

        // A distinctive term found by name is stronger evidence than a loose natural-language match.
        double Score(CodeReference reference) => namedFiles.Contains(reference.File)
            ? double.MaxValue
            : matchedTerms.TryGetValue(reference.File, out var terms) ? terms.Count - (terms.Contains("~context") ? 0.5 : 0) : 0;

        var candidates = named
            .Concat(relevant.References.Concat(searchHits).OrderByDescending(Score))
            .GroupBy(reference => (reference.File.ToLowerInvariant(), reference.Line))
            .Select(group => group.First())
            .GroupBy(reference => reference.File, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.Take(2))
            .ToList();
        var whereToWork = candidates.Take(MaxWhereToWork).ToList();

        var strongFiles = whereToWork
            .Select(reference => reference.File)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            // Only what the prompt itself names is confirmed. Search matches rank the list but stay unconfirmed:
            // on 15 real commits, files with two search signals were the right file 5 times in 30.
            .Where(namedFiles.Contains)
            .ToList();

        var testFiles = new List<string>();
        foreach (var file in whereToWork.Select(reference => reference.File).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (testFiles.Count >= MaxTestFiles)
            {
                break;
            }

            testFiles.AddRange(provider.Ask(p => p.FindTests(root, file)).References.Select(reference => reference.File));
        }

        testFiles = testFiles.Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxTestFiles).ToList();

        var technologies = request.Repository.TechnologyProfile?.Technologies ?? Array.Empty<Integration.RepositoryScanning.TechnologyEvidence>();
        foreach (var technology in technologies)
        {
            // A stack with no evidencing file is not worth a line; the build command below already implies it.
            if (ExistingFile(root, technology.Basis) is { } evidenceFile)
            {
                facts.Add(new EvidencedFact($"Detected stack: {technology.Name}", evidenceFile, null, "stack-detector"));
            }
        }

        // The command is a convention for the stack, not read from the repository; the renderer says so on its line.
        var command = BuildSystemDetector.DetectDefaultValidationCommand(technologies.Select(technology => technology.Name).ToList());
        var commandText = command is null ? null : (command.Value.Command + " " + string.Join(' ', command.Value.Arguments)).Trim();

        if (provider.UnavailableReason is not null)
        {
            assumptions.Insert(0, $"Code intelligence unavailable: {provider.UnavailableReason.TrimEnd('.')}. File and symbol locations below were not verified.");
        }

        if (provider.UnavailableReason is null && identifiers.Count == 0 && candidates.Count == 0)
        {
            questions.Add("The prompt does not name a file, symbol or feature that could be located. What part of the repository is this about?");
        }

        var checks = new List<string>();
        if (commandText is not null)
        {
            checks.Add($"`{commandText}` exits 0");
        }

        if (strongFiles.Count > 0)
        {
            checks.Add("Changes are confined to the files under Where to work, or each extra file is explained");
        }

        if (questions.Count > 0)
        {
            checks.Add("Every open question above is answered before editing");
        }

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
            candidates.Count - whereToWork.Count,
            strongFiles);
    }

    private static void GroundFile(string root, string identifier, List<EvidencedFact> facts, List<string> assumptions, List<string> questions, List<CodeReference> named)
    {
        var (matches, searchedEverything) = LocateFiles(root, identifier);
        if (matches.Count == 1 && facts.Any(fact => fact.Source == "filesystem" && string.Equals(fact.File, matches[0], StringComparison.OrdinalIgnoreCase)))
        {
            // The same file named two ways ("picks.py" and "app/picks.py") is one fact.
        }
        else if (matches.Count == 1)
        {
            facts.Add(new EvidencedFact($"`{identifier}` exists", matches[0], null, "filesystem"));
            named.Add(new CodeReference(Path.GetFileName(matches[0]), "file", matches[0], 0, null, "filesystem"));
        }
        else if (matches.Count > 1)
        {
            questions.Add($"The prompt mentions `{identifier}`, which matches several files: {string.Join(", ", matches)}. Which did you mean?");
        }
        else if (!searchedEverything)
        {
            assumptions.Add($"`{identifier}` is named in the prompt but the repository could not be fully searched for it.");
        }
        else if (PromptIdentifierExtractor.IsDeliberateFileReference(identifier))
        {
            questions.Add(NotFound(identifier, Array.Empty<CodeReference>()));
        }
    }

    private static void GroundSymbol(string root, string identifier, Guarded provider, List<EvidencedFact> facts, List<string> assumptions, List<string> questions, List<CodeReference> named)
    {
        // "OrderService.Submit" is indexed under its member and its type, not the dotted form.
        var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var memberName = parts[^1];
        var memberLookup = provider.Ask(p => p.FindSymbol(root, memberName, 10));
        var typeLookup = parts.Length > 1 ? provider.Ask(p => p.FindSymbol(root, parts[0], 10)) : null;
        if (!memberLookup.Available || typeLookup is { Available: false })
        {
            assumptions.Add($"`{identifier}` is named in the prompt but could not be checked against the repository.");
            return;
        }

        static bool Named(CodeReference reference, string name) => string.Equals(reference.Name, name, StringComparison.OrdinalIgnoreCase);
        var exact = memberLookup.References.Where(reference => Named(reference, memberName)).ToList();
        var closest = memberLookup.References.Concat(typeLookup?.References ?? Array.Empty<CodeReference>()).ToList();

        if (typeLookup is not null)
        {
            // The member counts only where its type is defined too; a same-named member elsewhere proves nothing.
            var typeFiles = new HashSet<string>(typeLookup.References.Where(reference => Named(reference, parts[0])).Select(reference => reference.File), StringComparer.OrdinalIgnoreCase);
            exact = exact.Where(reference => typeFiles.Contains(reference.File)).ToList();
        }

        var onDisk = exact.Where(reference => ExistingFile(root, reference.File) is not null).ToList();
        foreach (var stale in exact.Except(onDisk))
        {
            assumptions.Add($"`{identifier}` was found by the index at {stale.File}:{stale.Line}, but that file is not on disk - the index may be stale.");
        }

        var files = onDisk.Select(reference => reference.File).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (files.Count == 1)
        {
            foreach (var reference in onDisk.Take(2))
            {
                facts.Add(new EvidencedFact($"`{reference.Name}` is a {reference.Kind}", reference.File, reference.Line, reference.Source));
                named.Add(reference);
            }
        }
        else if (files.Count > 1)
        {
            var places = string.Join(", ", onDisk.Take(5).Select(reference => $"{reference.File}:{reference.Line}"));
            questions.Add($"The prompt mentions `{identifier}`, which is defined in several places: {places}. Which did you mean?");
        }
        else if (exact.Count == 0)
        {
            questions.Add(NotFound(identifier, closest));
        }
    }

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

    // The repository-relative path with forward slashes when that file is on disk inside the root; otherwise null.
    private static string? ExistingFile(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return null;
        }

        try
        {
            var full = Path.GetFullPath(Path.Combine(root, relativePath));
            return IsInside(root, full) && File.Exists(full) ? relativePath.Replace('\\', '/') : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return null;
        }
    }

    private static bool IsInside(string root, string fullPath) =>
        fullPath.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    // A path is checked where it points. A bare file name is searched for, bounded, skipping build and dependency
    // folders, and every match is returned: two files of the same name are an ambiguity, not a location.
    private static (IReadOnlyList<string> Matches, bool SearchedEverything) LocateFiles(string root, string identifier)
    {
        var relative = identifier.Replace('\\', '/').TrimStart('/');
        if (relative.StartsWith("./", StringComparison.Ordinal))
        {
            relative = relative[2..];
        }

        var matches = new List<string>();
        try
        {
            var full = Path.GetFullPath(Path.Combine(root, relative));
            if (IsInside(root, full) && (File.Exists(full) || Directory.Exists(full)))
            {
                return (new[] { Path.GetRelativePath(root, full).Replace('\\', '/') }, true);
            }

            if (relative.Contains('/'))
            {
                return (matches, true);
            }

            var visited = 0;
            var pending = new Queue<string>();
            pending.Enqueue(Path.GetFullPath(root));
            while (pending.Count > 0)
            {
                var directory = pending.Dequeue();
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    if (++visited > MaxFileSearchEntries)
                    {
                        return (matches, false);
                    }

                    if (string.Equals(Path.GetFileName(file), relative, StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
                        if (matches.Count >= MaxFileMatches)
                        {
                            return (matches, true);
                        }
                    }
                }

                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    if (!IgnoredDirectories.Contains(Path.GetFileName(child)))
                    {
                        pending.Enqueue(child);
                    }
                }
            }

            return (matches, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return (matches, false);
        }
    }
}
