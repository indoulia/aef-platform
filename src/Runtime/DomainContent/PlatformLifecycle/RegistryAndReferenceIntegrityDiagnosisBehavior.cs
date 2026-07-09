using System.Text.RegularExpressions;
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.PlatformLifecycle;

// The executable realization of SKL-EXE-0046 - the one genuinely new Repair
// diagnostic the design owns. Scans every Markdown file under the
// repository root for governed frontmatter (`Id:`, `Depends On:`,
// `Used By:`, `Supersedes:`, each a plain line carrying a bracketed,
// comma-separated list per this repository's own real convention) and
// resolves each cited token either against a declared `Id:` elsewhere in
// the scanned set, or, for a path-shaped token, against real file
// existence - never a fabricated PASS. Findings are cited by Id and
// file:line (Responsibility 4's own postcondition). This is read-only text
// analysis - Finding F5 (file-write capability) does not apply here, so
// this Behavior is genuinely realizable without any new I/O capability,
// unlike Maintenance's own registry-comparison dimensions. The Runtime
// Registry's own single-Id-lookup-only query surface (Reuse Ledger) is a
// real, named, inherited limitation - this Behavior never claims to check
// live Registry registration, only reports that gap once per invocation.
public sealed class RegistryAndReferenceIntegrityDiagnosisBehavior : IBehavior
{
    private static readonly Regex IdLine = new(@"^Id:\s*(\S+)", RegexOptions.Compiled);
    private static readonly Regex ReferenceLine = new(@"^(Depends On|Used By|Supersedes):\s*\[(.*)\]", RegexOptions.Compiled);

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("RegistryAndReferenceIntegrityDiagnosisRequest", out var raw) || raw is not RegistryAndReferenceIntegrityDiagnosisRequest request)
        {
            return new RegistryAndReferenceIntegrityDiagnosisResult(Array.Empty<ReferenceIntegrityFinding>(), new[] { "No registry and reference integrity diagnosis request was supplied." });
        }

        var root = request.RepositoryContext.RepositoryRoot;
        if (!Directory.Exists(root))
        {
            return new RegistryAndReferenceIntegrityDiagnosisResult(Array.Empty<ReferenceIntegrityFinding>(), new[] { $"Repository root '{root}' is not readable - no reference scan was performed." });
        }

        var markdownFiles = Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories).ToList();
        var declaredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in markdownFiles)
        {
            foreach (var line in File.ReadLines(file))
            {
                var match = IdLine.Match(line);
                if (match.Success)
                {
                    declaredIds.Add(match.Groups[1].Value);
                }
            }
        }

        var findings = new List<ReferenceIntegrityFinding>();
        foreach (var file in markdownFiles)
        {
            var lines = File.ReadAllLines(file);
            for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                var match = ReferenceLine.Match(lines[lineNumber]);
                if (!match.Success)
                {
                    continue;
                }

                var field = match.Groups[1].Value;
                var tokens = match.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var relativePath = Path.GetRelativePath(root, file);
                foreach (var token in tokens)
                {
                    var citation = $"{field} '{token}' in {relativePath}:{lineNumber + 1}";
                    var resolves = LooksLikeAPath(token)
                        ? ResolvesWithinRoot(root, token)
                        : declaredIds.Contains(token);

                    findings.Add(new ReferenceIntegrityFinding(
                        citation,
                        resolves ? "PASS" : "FAIL",
                        resolves ? "Resolves to a declared Id or an existing file." : "Does not resolve to any declared Id or existing file in this repository."));
                }
            }
        }

        var openItems = new List<string>
        {
            "Registry registration was not checked - the Runtime Registry's own query surface is single-Id lookup only (no enumerate-all), the same inherited limitation this design's Reuse Ledger already names; only reference resolution against scanned Markdown frontmatter was performed.",
        };

        return new RegistryAndReferenceIntegrityDiagnosisResult(findings, openItems);
    }

    private static bool LooksLikeAPath(string token) => token.Contains('/') || token.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    // A path-shaped token is untrusted content lifted from scanned file
    // text, never a trusted caller input - resolving it against real file
    // existence must stay confined to the repository root (canonicalized,
    // trailing-separator-qualified so a sibling directory sharing root's
    // own name prefix can never match), rejecting both an absolute-path
    // token and a "../" traversal outside it, rather than treating either
    // as a legitimate citation the way a same-directory relative path is.
    private static bool ResolvesWithinRoot(string root, string token)
    {
        var canonicalRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root, token));
        return candidate.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate);
    }
}
