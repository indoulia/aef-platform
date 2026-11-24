namespace Runtime.Integration.RepositoryScanning;

// Turns S22's raw, read-only directory listings into a Technology
// Inventory (SCH-PRJ-0002 v2.0.0's `Technologies` field, ai-engineering-
// framework repo) - without ever guessing (Epic 0008, Story S23), without
// ever performing an unbounded walk of the whole tree (Epic 0008, Story
// S27), and - as of Story S28 (ADR-0013) - without ever collapsing
// "confirmed nothing here," "could not tell," and "found more than one"
// into the same signal (docs/aep/ARB-Repository-Technology-Domain-Model.md
// §1.2, INV-2/INV-3). A cross-stack match is the normal, evidenced Hybrid
// case, not an unresolved tie to be broken.
//
// Detection walks a bounded number of directory levels beneath the root
// - see MaxDepthBeneathRoot below - skipping a fixed, narrow list of
// well-known non-source directories - see IgnoredDirectoryNames below -
// entirely (never descended into). Every listing still goes through
// S22's own IRepositoryScanner.ListEntries contract at deeper relative
// paths this class constructs itself; IRepositoryScanner and
// LocalFileSystemRepositoryScanner are both unmodified and unaware that
// more than one call is ever made (Story S27 acceptance criterion 2).
//
// A stack is recognized by any marker file's exact name or extension
// being present, as a file (never a directory), anywhere within the
// bounded traversal:
//
// | Stack   | Marker file(s)                                     |
// |---------|-----------------------------------------------------|
// | dotnet  | *.csproj, *.sln                                    |
// | node    | package.json                                       |
// | python  | pyproject.toml, requirements.txt, setup.py         |
// | java    | pom.xml, build.gradle                              |
// | rust    | Cargo.toml                                         |
// | go      | go.mod                                             |
//
// Multiple marker files for the *same* stack - whether both at the root
// or spread across several directories within the bound - is still a
// single Technology Inventory entry. Marker files for *two or more
// different* stacks anywhere within the bound is Detected with every
// matched entry present (Story S28 - reverses S23's prior "unresolved
// tie" treatment of this exact case). A listing failure anywhere within
// the bound (a reason unrelated to content) makes the whole detection
// Inconclusive - it never silently contributes nothing. Only a fully
// completed traversal that finds zero matches, with no listing failure
// anywhere within it, is ConfirmedEmpty.
public static class StackDetector
{
    // Hard scope boundary (Epic 0008, Story S27): the traversal descends
    // at most this many directory levels beneath the root (root itself
    // is level 0). Fixed, documented, and not configurable via any
    // public parameter or Input field - deliberately small so this stays
    // a narrow technology-detection check, never general-purpose file
    // indexing.
    private const int MaxDepthBeneathRoot = 3;

    // Fixed, narrow list of well-known non-source directory names that
    // are skipped entirely - never descended into, never listed - no
    // matter how shallow. This is this Story's single source of truth
    // for the ignore-list; nowhere else in this file names one of these
    // directories.
    //
    // | Ignored directory names                                        |
    // |-----------------------------------------------------------------|
    // | .git, bin, obj, node_modules, dist, build, .vs, .idea, target,  |
    // | vendor, .venv, __pycache__                                     |
    private static readonly IReadOnlySet<string> IgnoredDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", "node_modules", "dist", "build", ".vs", ".idea", "target", "vendor", ".venv", "__pycache__",
    };

    private static readonly IReadOnlyList<(string Stack, Func<string, bool> IsMarker)> MarkerRules = new (string, Func<string, bool>)[]
    {
        ("dotnet", name => name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)),
        ("node", name => name.Equals("package.json", StringComparison.OrdinalIgnoreCase)),
        ("python", name => name.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase)
            || name.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase)
            || name.Equals("setup.py", StringComparison.OrdinalIgnoreCase)),
        ("java", name => name.Equals("pom.xml", StringComparison.OrdinalIgnoreCase) || name.Equals("build.gradle", StringComparison.OrdinalIgnoreCase)),
        ("gradle", name => name.EndsWith(".gradle.kts", StringComparison.OrdinalIgnoreCase)),
        ("rust", name => name.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase)),
        ("go", name => name.Equals("go.mod", StringComparison.OrdinalIgnoreCase)),
    };

    // Returns the evidenced Technology Inventory (Detected, with every
    // matched stack present), a confirmed absence (ConfirmedEmpty), or
    // Inconclusive when the traversal itself - including the root listing
    // - could not fully complete for a reason unrelated to content.
    public static TechnologyDetectionResult DetectTechnologies(IRepositoryScanner scanner)
    {
        var state = new DetectionState();
        CollectMatchedStacks(scanner, string.Empty, depth: 0, state);

        if (state.Inconclusive)
        {
            return TechnologyDetectionResult.Inconclusive;
        }

        if (state.MatchedStackLocations.Count == 0)
        {
            return TechnologyDetectionResult.ConfirmedEmpty;
        }

        var technologies = state.MatchedStackLocations
            .Select(pair => new TechnologyEvidence(pair.Key, DescribeBasis(pair.Value)))
            .ToList();

        return TechnologyDetectionResult.Detected(technologies);
    }

    // Lists relativePath (S22's existing, unmodified ListEntries), records
    // any marker matches found there, then - unless the bound has been
    // reached - recurses into every non-ignored subdirectory found, one
    // level deeper each time. A listing failure at any depth (including
    // the root) marks the whole detection Inconclusive and stops further
    // recursion down that branch - it is never treated as "nothing here."
    private static void CollectMatchedStacks(IRepositoryScanner scanner, string relativePath, int depth, DetectionState state)
    {
        var result = scanner.ListEntries(relativePath);
        if (result.Status != RepositoryScanStatus.Success)
        {
            state.Inconclusive = true;
            return;
        }

        foreach (var rule in MarkerRules)
        {
            if (!result.Entries.Any(entry => !entry.IsDirectory && rule.IsMarker(entry.Name)))
            {
                continue;
            }

            if (!state.MatchedStackLocations.TryGetValue(rule.Stack, out var locations))
            {
                locations = new List<string>();
                state.MatchedStackLocations[rule.Stack] = locations;
            }

            locations.Add(relativePath.Length == 0 ? "repository root" : relativePath);
        }

        if (depth >= MaxDepthBeneathRoot)
        {
            return;
        }

        foreach (var entry in result.Entries)
        {
            if (!entry.IsDirectory || IgnoredDirectoryNames.Contains(entry.Name))
            {
                continue;
            }

            var childPath = relativePath.Length == 0 ? entry.Name : Path.Combine(relativePath, entry.Name);
            CollectMatchedStacks(scanner, childPath, depth + 1, state);
        }
    }

    private static string DescribeBasis(IReadOnlyList<string> locations) =>
        $"marker file(s) found at: {string.Join(", ", locations)}";

    private sealed class DetectionState
    {
        public Dictionary<string, List<string>> MatchedStackLocations { get; } = new(StringComparer.Ordinal);

        public bool Inconclusive { get; set; }
    }
}
