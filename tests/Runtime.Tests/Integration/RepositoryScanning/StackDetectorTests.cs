using Runtime.Integration.RepositoryScanning;
using Xunit;

namespace Runtime.Tests.Integration.RepositoryScanning;

// Epic 0008 (epics/0008-local-repository-scanner-implementation.md), Story
// S23: turning S22's raw, read-only directory listings into the same
// PrimaryStack fact PCT-EXE-0015 already consumes, without ever guessing.
// Extended by Story S27 to cover the bounded-recursion behavior added
// there (max-depth constant + fixed ignore-list) - see the tests below
// named for bounded depth, ignored directories, and cross-depth ties.
// Fixtures are real, minimal, temporary on-disk directories per test - one
// .csproj, one package.json, one directory with both, one empty directory -
// never a synthetic in-memory fake, per this Story's own Validation
// strategy.
public sealed class StackDetectorTests : IDisposable
{
    private readonly DirectoryInfo _root;

    public StackDetectorTests()
    {
        _root = Directory.CreateTempSubdirectory("s23-stack-detector-tests-");
    }

    public void Dispose()
    {
        try
        {
            _root.Delete(recursive: true);
        }
        catch
        {
            // Best-effort cleanup only - never fail a test on teardown.
        }
    }

    private LocalFileSystemRepositoryScanner Scanner() => new(_root.FullName);

    // Acceptance criterion 1: exactly one recognized stack's marker file ->
    // that stack, unambiguously.
    [Fact]
    public void SingleDotNetMarker_ReturnsDotnetUnambiguously()
    {
        File.WriteAllText(Path.Combine(_root.FullName, "Sample.csproj"), "<Project />");

        var stack = StackDetector.DetectPrimaryStack(Scanner());

        Assert.Equal("dotnet", stack);
    }

    [Fact]
    public void SingleNodeMarker_ReturnsNodeUnambiguously()
    {
        File.WriteAllText(Path.Combine(_root.FullName, "package.json"), "{}");

        var stack = StackDetector.DetectPrimaryStack(Scanner());

        Assert.Equal("node", stack);
    }

    // Note (per this Story's own acceptance criteria): multiple marker
    // files for the *same* stack is still a single-stack, unambiguous
    // case - AC1, not AC2's tie.
    [Fact]
    public void MultipleMarkersForTheSameStack_StillReturnsThatStackUnambiguously()
    {
        File.WriteAllText(Path.Combine(_root.FullName, "Sample.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(_root.FullName, "Sample.sln"), "Microsoft Visual Studio Solution File");

        var stack = StackDetector.DetectPrimaryStack(Scanner());

        Assert.Equal("dotnet", stack);
    }

    // Acceptance criterion 2: markers for two or more *different* stacks,
    // with no documented precedence rule - undetermined, never an
    // arbitrary pick.
    [Fact]
    public void MarkersForTwoDifferentStacks_ReturnsUndetermined_NeverGuesses()
    {
        File.WriteAllText(Path.Combine(_root.FullName, "Sample.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(_root.FullName, "package.json"), "{}");

        var stack = StackDetector.DetectPrimaryStack(Scanner());

        Assert.Null(stack);
    }

    // Acceptance criterion 3: no recognized marker file anywhere at the
    // root - undetermined.
    [Fact]
    public void NoRecognizedMarker_ReturnsUndetermined()
    {
        File.WriteAllText(Path.Combine(_root.FullName, "README.md"), "hello");

        var stack = StackDetector.DetectPrimaryStack(Scanner());

        Assert.Null(stack);
    }

    [Fact]
    public void EmptyDirectory_ReturnsUndetermined()
    {
        var stack = StackDetector.DetectPrimaryStack(Scanner());

        Assert.Null(stack);
    }

    // Detection is by marker *file* - a directory that happens to share a
    // marker's exact name (e.g. a "package.json" directory) is not a
    // marker file and must not be mistaken for one.
    [Fact]
    public void MarkerNameThatIsActuallyADirectory_IsNotTreatedAsAMarker()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "package.json"));

        var stack = StackDetector.DetectPrimaryStack(Scanner());

        Assert.Null(stack);
    }

    // Story S27 acceptance criterion 1: a marker within the bounded depth
    // beneath the root - even though the root itself has no recognized
    // marker - is now found unambiguously. (Superseded S23 behavior: prior
    // to S27, StackDetector only ever looked at the root's immediate
    // entries; this nested case used to return undetermined.)
    [Fact]
    public void MarkerNestedWithinBoundedDepth_IsDetected()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "nested"));
        File.WriteAllText(Path.Combine(_root.FullName, "nested", "package.json"), "{}");

        var stack = StackDetector.DetectPrimaryStack(Scanner());

        Assert.Equal("node", stack);
    }

    // Story S27 DoD example: a marker two levels beneath an otherwise
    // markerless root is still within the bound and is detected.
    [Fact]
    public void MarkerTwoLevelsBeneathRoot_IsDetected()
    {
        var nested = Path.Combine(_root.FullName, "src", "app");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Sample.csproj"), "<Project />");

        var stack = StackDetector.DetectPrimaryStack(Scanner());

        Assert.Equal("dotnet", stack);
    }

    // Story S27 acceptance criterion 2: the traversal is bounded by a
    // fixed max depth - a marker placed beyond it (four levels beneath the
    // root) is never seen, proving the bound is real and not just
    // documented.
    [Fact]
    public void MarkerBeyondBoundedDepth_ReturnsUndetermined()
    {
        var beyondBound = Path.Combine(_root.FullName, "a", "b", "c", "d");
        Directory.CreateDirectory(beyondBound);
        File.WriteAllText(Path.Combine(beyondBound, "package.json"), "{}");

        var stack = StackDetector.DetectPrimaryStack(Scanner());

        Assert.Null(stack);
    }

    // Story S27 acceptance criterion 2: a fixed, narrow ignore-list is
    // never descended into - a marker inside one of those directories
    // (e.g. node_modules, however shallow) is not treated as a hit.
    [Fact]
    public void MarkerInsideIgnoredDirectory_IsNotDetected()
    {
        var nodeModules = Path.Combine(_root.FullName, "node_modules");
        Directory.CreateDirectory(nodeModules);
        File.WriteAllText(Path.Combine(nodeModules, "Sample.csproj"), "<Project />");

        var stack = StackDetector.DetectPrimaryStack(Scanner());

        Assert.Null(stack);
    }

    // Story S27 acceptance criterion 3: markers for two different stacks
    // discovered across two different subdirectories at different depths
    // within the bound is still an unresolved tie - not just a per-
    // directory check.
    [Fact]
    public void CrossStackTieAcrossDifferentSubdirectoriesAndDepths_ReturnsUndetermined()
    {
        var dotnetDir = Path.Combine(_root.FullName, "backend");
        Directory.CreateDirectory(dotnetDir);
        File.WriteAllText(Path.Combine(dotnetDir, "Sample.csproj"), "<Project />");

        var nodeDir = Path.Combine(_root.FullName, "frontend", "app");
        Directory.CreateDirectory(nodeDir);
        File.WriteAllText(Path.Combine(nodeDir, "package.json"), "{}");

        var stack = StackDetector.DetectPrimaryStack(Scanner());

        Assert.Null(stack);
    }

    // Story S27 DoD: a deep/wide fixture deliberately exceeding the bound
    // in both breadth (many root-level siblings) and depth (a chain with
    // wide fan-out beyond the max depth) still completes quickly, proving
    // the bound actually limits work done - not just the returned answer.
    [Fact]
    public void DeepAndWideFixtureExceedingTheBound_CompletesQuicklyWithoutHangingOrThrowing()
    {
        for (var i = 0; i < 100; i++)
        {
            Directory.CreateDirectory(Path.Combine(_root.FullName, $"sibling-{i}"));
        }

        var beyondBound = Path.Combine(_root.FullName, "a", "b", "c");
        Directory.CreateDirectory(beyondBound);
        for (var i = 0; i < 1000; i++)
        {
            Directory.CreateDirectory(Path.Combine(beyondBound, $"deep-{i}"));
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var stack = StackDetector.DetectPrimaryStack(Scanner());
        stopwatch.Stop();

        Assert.Null(stack);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Bounded traversal took too long: {stopwatch.Elapsed}");
    }

    [Fact]
    public void UnreadableRoot_ReturnsUndetermined_NeverThrows()
    {
        var missingRoot = Path.Combine(_root.FullName, "does-not-exist");
        var scanner = new LocalFileSystemRepositoryScanner(missingRoot);

        var stack = StackDetector.DetectPrimaryStack(scanner);

        Assert.Null(stack);
    }

    [Theory]
    [InlineData("pyproject.toml", "python")]
    [InlineData("requirements.txt", "python")]
    [InlineData("setup.py", "python")]
    [InlineData("pom.xml", "java")]
    [InlineData("build.gradle", "java")]
    [InlineData("Cargo.toml", "rust")]
    [InlineData("go.mod", "go")]
    public void SingleMarkerForEachRemainingStack_ReturnsThatStackUnambiguously(string markerFileName, string expectedStack)
    {
        File.WriteAllText(Path.Combine(_root.FullName, markerFileName), "marker");

        var stack = StackDetector.DetectPrimaryStack(Scanner());

        Assert.Equal(expectedStack, stack);
    }
}
