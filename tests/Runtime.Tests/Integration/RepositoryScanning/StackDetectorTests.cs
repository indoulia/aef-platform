using Runtime.Integration.RepositoryScanning;
using Xunit;

namespace Runtime.Tests.Integration.RepositoryScanning;

// Epic 0008 (epics/0008-local-repository-scanner-implementation.md), Story
// S23: turning S22's raw, read-only root directory listing into the same
// PrimaryStack fact PCT-EXE-0015 already consumes, without ever guessing.
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

    // Detection only looks at the root's immediate entries, per S22's own
    // contract (ListEntries does not recurse) - a marker nested in a
    // subdirectory must not be picked up.
    [Fact]
    public void MarkerNestedInSubdirectory_IsNotDetected()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "nested"));
        File.WriteAllText(Path.Combine(_root.FullName, "nested", "package.json"), "{}");

        var stack = StackDetector.DetectPrimaryStack(Scanner());

        Assert.Null(stack);
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
