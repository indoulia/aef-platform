namespace Runtime.Integration.RepositoryScanning;

// Turns S22's raw, read-only root directory listing into the same
// PrimaryStack fact PCT-EXE-0015 already consumes (RepositoryDescriptor.
// PrimaryStack: string?, "absent or empty signals an undetermined
// stack") - without ever guessing (Epic 0008, Story S23).
//
// Detection operates on the root directory's immediate entries only, per
// S22's own contract (IRepositoryScanner.ListEntries does not recurse). A
// stack is recognized by any marker file's exact name or extension being
// present, as a file (never a directory), at the root:
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
// Multiple marker files for the *same* stack (e.g. both a .csproj and a
// .sln) is still a single, unambiguous match (acceptance criterion 1).
// Marker files for *two or more different* stacks - with no documented
// precedence rule between them - is an unresolved tie (acceptance
// criterion 2), and a root with no recognized marker at all is
// unrecognized (acceptance criterion 3); both return null
// ("undetermined"), never an arbitrary pick.
public static class StackDetector
{
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
    // present, or markers for two or more different stacks are present
    // with no precedence rule to break the tie.
    public static string? DetectPrimaryStack(IRepositoryScanner scanner)
    {
        var result = scanner.ListEntries(string.Empty);
        if (result.Status != RepositoryScanStatus.Success)
        {
            return null;
        }

        var matchedStacks = MarkerRules
            .Where(rule => result.Entries.Any(entry => !entry.IsDirectory && rule.IsMarker(entry.Name)))
            .Select(rule => rule.Stack)
            .ToList();

        return matchedStacks.Count == 1 ? matchedStacks[0] : null;
    }
}
