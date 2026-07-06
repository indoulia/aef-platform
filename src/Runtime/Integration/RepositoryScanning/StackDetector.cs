namespace Runtime.Integration.RepositoryScanning;

// Turns S22's raw, read-only directory listings into the same
// PrimaryStack fact PCT-EXE-0015 already consumes (RepositoryDescriptor.
// PrimaryStack: string?, "absent or empty signals an undetermined
// stack") - without ever guessing (Epic 0008, Story S23), and without
// ever performing an unbounded walk of the whole tree (Epic 0008, Story
// S27).
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
// or spread across several directories within the bound (e.g. a .csproj
// at the root and another two levels down) - is still a single,
// unambiguous match (acceptance criterion 1). Marker files for *two or
// more different* stacks anywhere within the bound - with no documented
// precedence rule between them - is an unresolved tie (acceptance
// criterion 3), and finding no recognized marker anywhere within the
// bound is unrecognized (acceptance criterion 4); both return null
// ("undetermined"), never an arbitrary pick.
public static class StackDetector
{
    // Hard scope boundary (Epic 0008, Story S27): the traversal descends
    // at most this many directory levels beneath the root (root itself
    // is level 0). Fixed, documented, and not configurable via any
    // public parameter or Input field - deliberately small so this stays
    // a narrow "what's the primary stack" check, never general-purpose
    // file indexing.
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
        ("rust", name => name.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase)),
        ("go", name => name.Equals("go.mod", StringComparison.OrdinalIgnoreCase)),
    };

    // Returns the unambiguously-recognized primary stack, or null when
    // the root's listing cannot be read at all, no recognized marker is
    // present anywhere within the bounded traversal, or markers for two
    // or more different stacks are present within it with no precedence
    // rule to break the tie.
    public static string? DetectPrimaryStack(IRepositoryScanner scanner)
    {
        var matchedStacks = new HashSet<string>(StringComparer.Ordinal);
        CollectMatchedStacks(scanner, string.Empty, depth: 0, matchedStacks);

        return matchedStacks.Count == 1 ? matchedStacks.Single() : null;
    }

    // Lists relativePath (S22's existing, unmodified ListEntries), records
    // any marker matches found there, then - unless the bound has been
    // reached - recurses into every non-ignored subdirectory found, one
    // level deeper each time. Tie/no-match resolution happens once, in
    // DetectPrimaryStack, over the full accumulated set - never per
    // directory - so a tie discovered across two different subdirectories
    // at different depths is still a tie (acceptance criterion 5).
    private static void CollectMatchedStacks(IRepositoryScanner scanner, string relativePath, int depth, HashSet<string> matchedStacks)
    {
        var result = scanner.ListEntries(relativePath);
        if (result.Status != RepositoryScanStatus.Success)
        {
            return;
        }

        foreach (var rule in MarkerRules)
        {
            if (result.Entries.Any(entry => !entry.IsDirectory && rule.IsMarker(entry.Name)))
            {
                matchedStacks.Add(rule.Stack);
            }
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
            CollectMatchedStacks(scanner, childPath, depth + 1, matchedStacks);
        }
    }
}
