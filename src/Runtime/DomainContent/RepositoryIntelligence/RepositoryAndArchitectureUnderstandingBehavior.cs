using Runtime.Context;
using Runtime.Hosts.Skill;
using Runtime.Integration.RepositoryScanning;

namespace Runtime.DomainContent.RepositoryIntelligence;

// The executable realization of SKL-EXE-0030 (framework/execution/skills/
// repository-and-architecture-understanding.md)'s Behavior. The governed
// Skill spec is free-text/NL interpretation with no LLM integration point
// anywhere in this Runtime (same rationale as SKL-EXE-0021) - this is
// therefore a deterministic, evidence-only structural search: it answers
// whether a name mentioned in request_scope exists as a real top-level (or
// one-level-nested) directory/file entry, citing the real path as
// evidence, and reports UNKNOWN with an honest rationale otherwise -
// never a fabricated claim (PCT-EXE-0030's own postcondition).
public sealed class RepositoryAndArchitectureUnderstandingBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("RepositoryUnderstandingRequest", out var raw) || raw is not RepositoryUnderstandingRequest request)
        {
            return new RepositoryUnderstandingResult(
                new[] { new IntelligenceFinding("RAU-0001", "Repository Understanding", FindingClass.Unknown, "No request was supplied.", "n/a") },
                "No request was supplied to answer.");
        }

        if (!request.RepositoryContext.IsReadable)
        {
            var finding = new IntelligenceFinding("RAU-0001", "Repository Understanding", FindingClass.Unknown,
                $"Repository '{request.RepositoryContext.RepositoryName}' is not readable.", request.RepositoryContext.RepositoryRoot);
            return new RepositoryUnderstandingResult(new[] { finding }, "The repository could not be read.");
        }

        var scanner = new LocalFileSystemRepositoryScanner(request.RepositoryContext.RepositoryRoot);
        var match = FindMatch(scanner, request.RequestScope);

        if (match is null)
        {
            var finding = new IntelligenceFinding("RAU-0001", "Repository Understanding", FindingClass.Unknown,
                $"No top-level or one-level-nested entry matching '{request.RequestScope}' was found.",
                request.RepositoryContext.RepositoryRoot);
            return new RepositoryUnderstandingResult(new[] { finding }, $"Could not locate '{request.RequestScope}' within the scanned bound.");
        }

        var passFinding = new IntelligenceFinding("RAU-0001", "Repository Understanding", FindingClass.Pass,
            $"Found an entry matching '{request.RequestScope}'.", match);
        return new RepositoryUnderstandingResult(new[] { passFinding }, $"'{request.RequestScope}' exists at '{match}'.");
    }

    private static string? FindMatch(IRepositoryScanner scanner, string requestScope)
    {
        var rootListing = scanner.ListEntries(string.Empty);
        if (rootListing.Status != RepositoryScanStatus.Success)
        {
            return null;
        }

        foreach (var entry in rootListing.Entries)
        {
            if (entry.Name.Contains(requestScope, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Name;
            }

            if (entry.IsDirectory)
            {
                var nested = scanner.ListEntries(entry.Name);
                if (nested.Status == RepositoryScanStatus.Success && nested.Entries.Any(e => e.Name.Contains(requestScope, StringComparison.OrdinalIgnoreCase)))
                {
                    return entry.Name + " (contains a matching entry)";
                }
            }
        }

        return null;
    }
}
