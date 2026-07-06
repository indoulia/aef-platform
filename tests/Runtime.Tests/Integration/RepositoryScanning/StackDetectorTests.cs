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

    // Story S27 DoD: proves the depth bound actually limits the *work
    // done* (the number of real ListEntries calls made), not merely that
    // the returned answer happens to be correct or that one particular
    // fixture shape finishes quickly. A flat fan-out of siblings with no
    // children of their own cannot distinguish "the bound is enforced"
    // from "the bound was silently deleted" - an unbounded walk of a flat
    // fixture is just as fast as a bounded one, since there is nothing
    // beneath the first level to over-recurse into.
    //
    // This fixture instead has genuine exponential fan-out - branching
    // factor 4, continuing seven levels beneath the root - four levels
    // past MaxDepthBeneathRoot (3). A correctly depth-3-bounded traversal
    // visits only the first 85 directories (depths 0-3: 1+4+16+64); an
    // unbounded traversal of the very same fixture would have to visit
    // all 21,845 directories (depths 0-7) - two orders of magnitude more.
    // CountingRepositoryScanner below counts every real ListEntries call
    // made against a real LocalFileSystemRepositoryScanner over this real
    // temp-directory fixture - nothing here is mocked or faked; only the
    // call-counting is test-only instrumentation.
    [Fact]
    public void DeepAndWideFixtureExceedingTheBound_InstrumentedCallCount_ProvesBoundLimitsWorkDone()
    {
        const int branchingFactor = 4;
        const int levelsBeyondRoot = 7;
        CreateFanOutTree(_root.FullName, branchingFactor, levelsBeyondRoot);

        var expectedBoundedCallCount = 0;
        for (var depth = 0; depth <= 3; depth++)
        {
            expectedBoundedCallCount += (int)Math.Pow(branchingFactor, depth);
        }

        var totalDirectoryCount = 0;
        for (var depth = 0; depth <= levelsBeyondRoot; depth++)
        {
            totalDirectoryCount += (int)Math.Pow(branchingFactor, depth);
        }

        var scanner = new CountingRepositoryScanner(_root.FullName);

        var stack = StackDetector.DetectPrimaryStack(scanner);

        Assert.Null(stack);
        Assert.Equal(expectedBoundedCallCount, scanner.ListEntriesCallCount);
        Assert.True(
            scanner.ListEntriesCallCount < totalDirectoryCount / 100,
            $"Expected the bounded traversal ({scanner.ListEntriesCallCount} calls) to be at least two " +
            $"orders of magnitude fewer than the fixture's full {totalDirectoryCount} directories - an " +
            "unbounded traversal would visit all of them.");
    }

    // Builds a real on-disk tree with the given branching factor at every
    // level, continuing for remainingLevels levels beneath path (path
    // itself is not counted/created here - it is expected to already
    // exist, e.g. the test's own _root).
    private static void CreateFanOutTree(string path, int branchingFactor, int remainingLevels)
    {
        if (remainingLevels == 0)
        {
            return;
        }

        for (var i = 0; i < branchingFactor; i++)
        {
            var child = Path.Combine(path, $"d{i}");
            Directory.CreateDirectory(child);
            CreateFanOutTree(child, branchingFactor, remainingLevels - 1);
        }
    }

    // Test-only instrumentation (Story S27 review fix): delegates every
    // call to a real LocalFileSystemRepositoryScanner - unmodified,
    // unaware it is being counted - while recording how many times
    // ListEntries is actually invoked, so a test can assert on the amount
    // of real work performed rather than only on the final returned
    // value.
    private sealed class CountingRepositoryScanner : IRepositoryScanner
    {
        private readonly LocalFileSystemRepositoryScanner _inner;

        public CountingRepositoryScanner(string root)
        {
            _inner = new LocalFileSystemRepositoryScanner(root);
        }

        public int ListEntriesCallCount { get; private set; }

        public bool IsRootReadable() => _inner.IsRootReadable();

        public RepositoryScanResult ListEntries(string relativePath)
        {
            ListEntriesCallCount++;
            return _inner.ListEntries(relativePath);
        }
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
