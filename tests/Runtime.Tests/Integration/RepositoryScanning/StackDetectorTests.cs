using Runtime.Integration.RepositoryScanning;
using Xunit;

namespace Runtime.Tests.Integration.RepositoryScanning;

// Epic 0008 (epics/0008-local-repository-scanner-implementation.md), Story
// S23: turning S22's raw, read-only directory listings into an evidenced
// Technology Inventory, without ever guessing. Extended by Story S27 to
// cover bounded-recursion (max-depth constant + fixed ignore-list). Extended
// by Story S28 (ADR-0013, docs/aep/ARB-Repository-Technology-Domain-Model.md)
// to return a three-valued TechnologyDetectionResult - Detected/
// ConfirmedEmpty/Inconclusive - instead of a scalar `string?` that
// collapsed "confirmed nothing here" and "could not tell" into the same
// null, and that forced a cross-stack tie to be treated as an unresolved
// ambiguity rather than the normal, expected Hybrid case.
//
// Fixtures are real, minimal, temporary on-disk directories per test - one
// .csproj, one package.json, one directory with both, one empty directory,
// and (new for S28) one directory whose listing is made to fail via a
// test-only fault-injecting IRepositoryScanner - never a synthetic
// in-memory fake for the filesystem behavior itself, per this Story's own
// Validation strategy.
public sealed class StackDetectorTests : IDisposable
{
    private readonly DirectoryInfo _root;

    public StackDetectorTests()
    {
        _root = Directory.CreateTempSubdirectory("s28-stack-detector-tests-");
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

    private static string[] Names(TechnologyDetectionResult result) =>
        result.Technologies.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    // Acceptance criterion 1: exactly one recognized stack's marker file ->
    // Detected, unambiguously, with a non-empty Basis.
    [Fact]
    public void SingleDotNetMarker_ReturnsDetectedWithDotnet()
    {
        File.WriteAllText(Path.Combine(_root.FullName, "Sample.csproj"), "<Project />");

        var result = StackDetector.DetectTechnologies(Scanner());

        Assert.Equal(TechnologyDetectionStatus.Detected, result.Status);
        Assert.Equal(new[] { "dotnet" }, Names(result));
        Assert.False(string.IsNullOrWhiteSpace(result.Technologies.Single().Basis));
    }

    [Fact]
    public void SingleNodeMarker_ReturnsDetectedWithNode()
    {
        File.WriteAllText(Path.Combine(_root.FullName, "package.json"), "{}");

        var result = StackDetector.DetectTechnologies(Scanner());

        Assert.Equal(TechnologyDetectionStatus.Detected, result.Status);
        Assert.Equal(new[] { "node" }, Names(result));
    }

    // Note (per this Story's own acceptance criteria): multiple marker
    // files for the *same* stack is still a single-entry Detected result -
    // AC1, not a Hybrid case.
    [Fact]
    public void MultipleMarkersForTheSameStack_StillReturnsOneDetectedEntry()
    {
        File.WriteAllText(Path.Combine(_root.FullName, "Sample.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(_root.FullName, "Sample.sln"), "Microsoft Visual Studio Solution File");

        var result = StackDetector.DetectTechnologies(Scanner());

        Assert.Equal(TechnologyDetectionStatus.Detected, result.Status);
        Assert.Equal(new[] { "dotnet" }, Names(result));
    }

    // Story S28's own reversal of S23's original acceptance criterion 2:
    // markers for two or more *different* stacks are the normal, evidenced
    // Hybrid case - Detected with both entries - never collapsed into an
    // "undetermined" tie (docs/aep/ARB-Repository-Technology-Domain-Model.md
    // §1.2, INV-2). This is the exact real-world case (Ferret: .NET +
    // Node.js) that motivated ADR-0013.
    [Fact]
    public void MarkersForTwoDifferentStacks_ReturnsDetectedWithBoth_NeverCollapsesToUndetermined()
    {
        File.WriteAllText(Path.Combine(_root.FullName, "Sample.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(_root.FullName, "package.json"), "{}");

        var result = StackDetector.DetectTechnologies(Scanner());

        Assert.Equal(TechnologyDetectionStatus.Detected, result.Status);
        Assert.Equal(new[] { "dotnet", "node" }, Names(result));
        Assert.All(result.Technologies, t => Assert.False(string.IsNullOrWhiteSpace(t.Basis)));
    }

    // Acceptance criterion 3 (S23): no recognized marker file anywhere
    // within the bound, with a fully completed traversal - a confirmed,
    // confident absence, never conflated with "could not tell"
    // (INV-3).
    [Fact]
    public void NoRecognizedMarker_ReturnsConfirmedEmpty_NotInconclusive()
    {
        File.WriteAllText(Path.Combine(_root.FullName, "README.md"), "hello");

        var result = StackDetector.DetectTechnologies(Scanner());

        Assert.Equal(TechnologyDetectionStatus.ConfirmedEmpty, result.Status);
        Assert.Empty(result.Technologies);
    }

    [Fact]
    public void EmptyDirectory_ReturnsConfirmedEmpty_NotInconclusive()
    {
        var result = StackDetector.DetectTechnologies(Scanner());

        Assert.Equal(TechnologyDetectionStatus.ConfirmedEmpty, result.Status);
        Assert.Empty(result.Technologies);
    }

    // Detection is by marker *file* - a directory that happens to share a
    // marker's exact name (e.g. a "package.json" directory) is not a
    // marker file and must not be mistaken for one.
    [Fact]
    public void MarkerNameThatIsActuallyADirectory_IsNotTreatedAsAMarker_ReturnsConfirmedEmpty()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "package.json"));

        var result = StackDetector.DetectTechnologies(Scanner());

        Assert.Equal(TechnologyDetectionStatus.ConfirmedEmpty, result.Status);
    }

    // Story S27 acceptance criterion 1: a marker within the bounded depth
    // beneath the root - even though the root itself has no recognized
    // marker - is still Detected unambiguously.
    [Fact]
    public void MarkerNestedWithinBoundedDepth_IsDetected()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "nested"));
        File.WriteAllText(Path.Combine(_root.FullName, "nested", "package.json"), "{}");

        var result = StackDetector.DetectTechnologies(Scanner());

        Assert.Equal(TechnologyDetectionStatus.Detected, result.Status);
        Assert.Equal(new[] { "node" }, Names(result));
    }

    // Story S27 DoD example: a marker two levels beneath an otherwise
    // markerless root is still within the bound and is detected.
    [Fact]
    public void MarkerTwoLevelsBeneathRoot_IsDetected()
    {
        var nested = Path.Combine(_root.FullName, "src", "app");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Sample.csproj"), "<Project />");

        var result = StackDetector.DetectTechnologies(Scanner());

        Assert.Equal(TechnologyDetectionStatus.Detected, result.Status);
        Assert.Equal(new[] { "dotnet" }, Names(result));
    }

    // Story S27 acceptance criterion 2: the traversal is bounded by a
    // fixed max depth - a marker placed beyond it (four levels beneath the
    // root) is never seen. This is still a confirmed-empty-within-bound
    // result, not an inconclusive one - the bound itself is a documented
    // design limit, not a scan failure.
    [Fact]
    public void MarkerBeyondBoundedDepth_ReturnsConfirmedEmpty()
    {
        var beyondBound = Path.Combine(_root.FullName, "a", "b", "c", "d");
        Directory.CreateDirectory(beyondBound);
        File.WriteAllText(Path.Combine(beyondBound, "package.json"), "{}");

        var result = StackDetector.DetectTechnologies(Scanner());

        Assert.Equal(TechnologyDetectionStatus.ConfirmedEmpty, result.Status);
    }

    // Story S27 acceptance criterion 2: a fixed, narrow ignore-list is
    // never descended into - a marker inside one of those directories
    // (e.g. node_modules, however shallow) is not treated as a hit.
    [Fact]
    public void MarkerInsideIgnoredDirectory_IsNotDetected_ReturnsConfirmedEmpty()
    {
        var nodeModules = Path.Combine(_root.FullName, "node_modules");
        Directory.CreateDirectory(nodeModules);
        File.WriteAllText(Path.Combine(nodeModules, "Sample.csproj"), "<Project />");

        var result = StackDetector.DetectTechnologies(Scanner());

        Assert.Equal(TechnologyDetectionStatus.ConfirmedEmpty, result.Status);
    }

    // Story S27 acceptance criterion 3, re-stated for S28: markers for two
    // different stacks discovered across two different subdirectories at
    // different depths within the bound are still both Detected together -
    // the accumulation is over the *whole* bounded traversal, not
    // per-directory.
    [Fact]
    public void TwoDifferentStacksAcrossDifferentSubdirectoriesAndDepths_AreBothDetected()
    {
        var dotnetDir = Path.Combine(_root.FullName, "backend");
        Directory.CreateDirectory(dotnetDir);
        File.WriteAllText(Path.Combine(dotnetDir, "Sample.csproj"), "<Project />");

        var nodeDir = Path.Combine(_root.FullName, "frontend", "app");
        Directory.CreateDirectory(nodeDir);
        File.WriteAllText(Path.Combine(nodeDir, "package.json"), "{}");

        var result = StackDetector.DetectTechnologies(Scanner());

        Assert.Equal(TechnologyDetectionStatus.Detected, result.Status);
        Assert.Equal(new[] { "dotnet", "node" }, Names(result));
    }

    // Story S28: a subdirectory listing that fails for a reason unrelated
    // to what the repository actually contains (simulated here via a
    // test-only fault-injecting IRepositoryScanner, since OS-level
    // permission denial is not reliably portable in a unit test) makes
    // the *entire* detection Inconclusive - never silently treated as "no
    // marker here," and never conflated with ConfirmedEmpty (INV-2/INV-3).
    [Fact]
    public void SubdirectoryListingFails_ReturnsInconclusive_DistinctFromConfirmedEmpty()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "blocked"));
        var scanner = new FaultInjectingRepositoryScanner(_root.FullName, failAtRelativePath: "blocked");

        var result = StackDetector.DetectTechnologies(scanner);

        Assert.Equal(TechnologyDetectionStatus.Inconclusive, result.Status);
        Assert.Empty(result.Technologies);
    }

    // A genuinely unreadable root (the scanner's own IsRootReadable would
    // be false) reaching DetectTechnologies directly - defensive, since
    // real callers check IsRootReadable first (Epic 0008, Story S24) - is
    // Inconclusive, not ConfirmedEmpty: this method never had the chance
    // to confirm anything.
    [Fact]
    public void UnreadableRoot_ReturnsInconclusive_NeverThrows()
    {
        var missingRoot = Path.Combine(_root.FullName, "does-not-exist");
        var scanner = new LocalFileSystemRepositoryScanner(missingRoot);

        var result = StackDetector.DetectTechnologies(scanner);

        Assert.Equal(TechnologyDetectionStatus.Inconclusive, result.Status);
    }

    // Story S27 DoD: proves the depth bound actually limits the *work
    // done* (the number of real ListEntries calls made), not merely that
    // the returned answer happens to be correct. See the original S27
    // comment (preserved in git history) for the exponential-fan-out
    // rationale; unchanged by S28 beyond the return-shape assertion.
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

        var result = StackDetector.DetectTechnologies(scanner);

        Assert.Equal(TechnologyDetectionStatus.ConfirmedEmpty, result.Status);
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

    // Test-only fault injection (Story S28): wraps a real
    // LocalFileSystemRepositoryScanner but forces one specific relative
    // path's listing to fail with a structured, real RepositoryScanStatus -
    // exercising the Inconclusive outcome deterministically and portably,
    // without relying on OS-level permission APIs.
    private sealed class FaultInjectingRepositoryScanner : IRepositoryScanner
    {
        private readonly LocalFileSystemRepositoryScanner _inner;
        private readonly string _failAtRelativePath;

        public FaultInjectingRepositoryScanner(string root, string failAtRelativePath)
        {
            _inner = new LocalFileSystemRepositoryScanner(root);
            _failAtRelativePath = failAtRelativePath;
        }

        public bool IsRootReadable() => _inner.IsRootReadable();

        public RepositoryScanResult ListEntries(string relativePath)
        {
            if (relativePath == _failAtRelativePath)
            {
                return RepositoryScanResult.Failure(RepositoryScanStatus.RootNotReadable, "Simulated read failure for test purposes (Story S28 fault injection).");
            }

            return _inner.ListEntries(relativePath);
        }
    }

    [Theory]
    [InlineData("pyproject.toml", "python")]
    [InlineData("requirements.txt", "python")]
    [InlineData("setup.py", "python")]
    [InlineData("pom.xml", "java")]
    [InlineData("build.gradle", "java")]
    [InlineData("Cargo.toml", "rust")]
    [InlineData("go.mod", "go")]
    public void SingleMarkerForEachRemainingStack_ReturnsDetectedWithThatStack(string markerFileName, string expectedStack)
    {
        File.WriteAllText(Path.Combine(_root.FullName, markerFileName), "marker");

        var result = StackDetector.DetectTechnologies(Scanner());

        Assert.Equal(TechnologyDetectionStatus.Detected, result.Status);
        Assert.Equal(new[] { expectedStack }, Names(result));
    }
}
