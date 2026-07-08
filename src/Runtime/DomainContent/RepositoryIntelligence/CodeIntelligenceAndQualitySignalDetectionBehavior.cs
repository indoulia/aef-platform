using Runtime.Context;
using Runtime.Hosts.Skill;
using Runtime.Integration.RepositoryScanning;

namespace Runtime.DomainContent.RepositoryIntelligence;

// The executable realization of SKL-EXE-0031. AGT-EXE-0015's own Role #3
// explicitly sanctions "UNKNOWN-with-rationale otherwise" for any signal
// this Runtime has no Technology-Profile-resolved Automation for - so
// duplication/complexity/dead-code/anti-pattern detection (which require
// real static-analysis tooling this Runtime does not have) are reported
// UNKNOWN, never fabricated. The one signal genuinely spot-checkable with
// existing tools: whether a detected stack has an accompanying
// test-project/test-directory convention present (evidenced via the
// existing scanner - spot_checked: true because the absence/presence is
// independently verified against the real listing, per PCT-EXE-0031's own
// postcondition).
public sealed class CodeIntelligenceAndQualitySignalDetectionBehavior : IBehavior
{
    private static readonly string[] TestDirectoryMarkers = { "tests", "test", "spec", "specs" };

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("CodeIntelligenceRequest", out var raw) || raw is not CodeIntelligenceRequest request)
        {
            return new CodeIntelligenceResult(new[] { Unknown("CIQ-0001", "No request was supplied.") });
        }

        var findings = new List<IntelligenceFinding>();

        if (request.RepositoryContext.TechnologyProfile is null || request.RepositoryContext.TechnologyProfile.Technologies.Count == 0)
        {
            findings.Add(Unknown("CIQ-0001", "No Technology Profile is available - test-convention detection requires a detected stack."));
        }
        else
        {
            var scanner = new LocalFileSystemRepositoryScanner(request.RepositoryContext.RepositoryRoot);
            var rootListing = scanner.ListEntries(string.Empty);
            var hasTestDirectory = rootListing.Status == RepositoryScanStatus.Success
                && rootListing.Entries.Any(e => e.IsDirectory && TestDirectoryMarkers.Contains(e.Name, StringComparer.OrdinalIgnoreCase));

            findings.Add(hasTestDirectory
                ? new IntelligenceFinding("CIQ-0001", "Test Convention", FindingClass.Pass, "A conventional test directory was found at the repository root.", request.RepositoryContext.RepositoryRoot, SpotChecked: true)
                : new IntelligenceFinding("CIQ-0001", "Test Convention", FindingClass.Warning, "No conventional test directory (tests/test/spec/specs) was found at the repository root.", request.RepositoryContext.RepositoryRoot, SpotChecked: true));
        }

        findings.Add(Unknown("CIQ-0002", "No Technology-Profile-resolved Automation exists in this Runtime for technical debt, dead code, duplication, complexity, or anti-pattern detection - reported UNKNOWN rather than fabricated, per AGT-EXE-0015's own Role #3."));

        return new CodeIntelligenceResult(findings);
    }

    private static IntelligenceFinding Unknown(string id, string rationale) =>
        new(id, "Code Intelligence", FindingClass.Unknown, rationale, "n/a");
}
