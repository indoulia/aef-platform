using System.Reflection;
using Runtime.Integration.RepositoryScanning;
using Xunit;

namespace Runtime.Tests.Integration.RepositoryScanning;

// Epic 0008 (epics/0008-local-repository-scanner-implementation.md), Story
// S22: the Integration Adapter Boundary's first real external-I/O
// capability - a generic "read a repository's characteristics" contract
// with exactly one concrete binding (local filesystem, read-only,
// root-bounded). Fixtures are real, temporary on-disk directories created
// per test and cleaned up afterward, never a synthetic in-memory fake,
// per this Epic's own Validation strategy.
public sealed class LocalFileSystemRepositoryScannerTests : IDisposable
{
    private readonly DirectoryInfo _root;

    public LocalFileSystemRepositoryScannerTests()
    {
        _root = Directory.CreateTempSubdirectory("s22-scanner-tests-");
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

    // Acceptance criterion 1: no member of the contract itself accepts a
    // network address, credential, or process-execution argument of any
    // kind. This is a real, mechanical guarantee (reflection over the
    // interface's own method signatures), not a comment - per S22's
    // Definition of Done.
    [Fact]
    public void Contract_HasNoNetworkCredentialOrProcessExecutionMember()
    {
        var suspiciousKeywords = new[]
        {
            "uri", "url", "socket", "endpoint", "connection", "credential",
            "password", "secret", "token", "certificate", "processstartinfo",
            "process", "network", "http", "ftp", "ssh",
        };

        var interfaceType = typeof(IRepositoryScanner);
        var parameterTypes = interfaceType.GetMethods()
            .SelectMany(m => m.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        foreach (var parameterType in parameterTypes)
        {
            var typeName = parameterType.Name.ToLowerInvariant();
            Assert.False(
                suspiciousKeywords.Any(keyword => typeName.Contains(keyword)),
                $"Parameter type '{parameterType.Name}' looks like a network/credential/process-execution shape, which IRepositoryScanner must never expose.");
        }

        // The only types this contract may use to describe "what to read"
        // are plain, inert data - never something capable of addressing a
        // remote system or spawning a process.
        var allowedParameterTypes = new[] { typeof(string) };
        Assert.All(parameterTypes, t => Assert.Contains(t, allowedParameterTypes));
    }

    [Fact]
    public void RootThatExistsAndIsReadable_IsReportedAsReadable()
    {
        var scanner = new LocalFileSystemRepositoryScanner(_root.FullName);

        Assert.True(scanner.IsRootReadable());
    }

    [Fact]
    public void RootThatDoesNotExist_IsReportedAsNotReadable_AndListingFails()
    {
        var missingRoot = Path.Combine(_root.FullName, "does-not-exist");
        var scanner = new LocalFileSystemRepositoryScanner(missingRoot);

        Assert.False(scanner.IsRootReadable());

        var result = scanner.ListEntries(string.Empty);

        Assert.Equal(RepositoryScanStatus.RootNotReadable, result.Status);
        Assert.Empty(result.Entries);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public void ValidInBoundsPath_ReturnsReadOnlyListing_NamesAndIsDirectoryOnly()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "src"));
        File.WriteAllText(Path.Combine(_root.FullName, "README.md"), "hello");
        var scanner = new LocalFileSystemRepositoryScanner(_root.FullName);

        var result = scanner.ListEntries(string.Empty);

        Assert.Equal(RepositoryScanStatus.Success, result.Status);
        Assert.Null(result.FailureReason);
        Assert.Contains(result.Entries, e => e.Name == "src" && e.IsDirectory);
        Assert.Contains(result.Entries, e => e.Name == "README.md" && !e.IsDirectory);
    }

    [Fact]
    public void ValidInBoundsSubdirectory_ReturnsOnlyThatDirectorysImmediateEntries()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "src", "nested"));
        File.WriteAllText(Path.Combine(_root.FullName, "src", "Program.cs"), "// content");
        var scanner = new LocalFileSystemRepositoryScanner(_root.FullName);

        var result = scanner.ListEntries("src");

        Assert.Equal(RepositoryScanStatus.Success, result.Status);
        Assert.Equal(2, result.Entries.Count);
        Assert.Contains(result.Entries, e => e.Name == "nested" && e.IsDirectory);
        Assert.Contains(result.Entries, e => e.Name == "Program.cs" && !e.IsDirectory);
    }

    [Fact]
    public void FileContentIsNeverRead_EvenWhenAFileCannotBeOpenedForReading()
    {
        var lockedPath = Path.Combine(_root.FullName, "locked.bin");
        File.WriteAllText(lockedPath, "content nobody should read for a listing");

        // Hold an exclusive lock, denying any read of this file's content.
        using var exclusiveHandle = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var scanner = new LocalFileSystemRepositoryScanner(_root.FullName);

        var result = scanner.ListEntries(string.Empty);

        Assert.Equal(RepositoryScanStatus.Success, result.Status);
        Assert.Contains(result.Entries, e => e.Name == "locked.bin" && !e.IsDirectory);
    }

    [Fact]
    public void NonExistentSubPath_ReturnsPathNotFoundFailure()
    {
        var scanner = new LocalFileSystemRepositoryScanner(_root.FullName);

        var result = scanner.ListEntries("no-such-directory");

        Assert.Equal(RepositoryScanStatus.PathNotFound, result.Status);
        Assert.Empty(result.Entries);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public void DotDotTraversal_RefusesWithStructuredFailure_NeverThrowsNeverTruncates()
    {
        var outside = Directory.CreateDirectory(Path.Combine(_root.Parent!.FullName, "s22-outside-" + Guid.NewGuid().ToString("N")));
        try
        {
            File.WriteAllText(Path.Combine(outside.FullName, "secret.txt"), "should never be listed");
            var scanner = new LocalFileSystemRepositoryScanner(_root.FullName);

            var result = scanner.ListEntries(Path.Combine("..", outside.Name));

            Assert.Equal(RepositoryScanStatus.PathEscapesRoot, result.Status);
            Assert.Empty(result.Entries);
            Assert.NotNull(result.FailureReason);
        }
        finally
        {
            outside.Delete(recursive: true);
        }
    }

    [Fact]
    public void AbsolutePathOutsideRoot_RefusesWithStructuredFailure()
    {
        var outside = Directory.CreateTempSubdirectory("s22-outside-absolute-");
        try
        {
            var scanner = new LocalFileSystemRepositoryScanner(_root.FullName);

            var result = scanner.ListEntries(outside.FullName);

            Assert.Equal(RepositoryScanStatus.PathEscapesRoot, result.Status);
            Assert.Empty(result.Entries);
        }
        finally
        {
            outside.Delete(recursive: true);
        }
    }

    [Fact]
    public void ReparsePointResolvingOutsideRoot_RefusesWithStructuredFailure_NeverFollowsTheLinkToListItsRealContent()
    {
        var outside = Directory.CreateDirectory(Path.Combine(_root.Parent!.FullName, "s22-link-target-" + Guid.NewGuid().ToString("N")));
        try
        {
            File.WriteAllText(Path.Combine(outside.FullName, "outside-secret.txt"), "should never be listed");

            var linkPath = Path.Combine(_root.FullName, "escape-link");
            // A junction is the real, non-simulated NTFS reparse point this
            // sandbox can create without elevated/Developer-Mode privilege;
            // .NET resolves it through the same ResolveLinkTarget API path
            // as a true directory symbolic link, so it exercises the exact
            // production code path a symlink escape would.
            CreateDirectoryJunction(linkPath, outside.FullName);

            var scanner = new LocalFileSystemRepositoryScanner(_root.FullName);

            var result = scanner.ListEntries("escape-link");

            Assert.Equal(RepositoryScanStatus.PathEscapesRoot, result.Status);
            Assert.Empty(result.Entries);
            Assert.NotNull(result.FailureReason);
        }
        finally
        {
            outside.Delete(recursive: true);
        }
    }

    private static void CreateDirectoryJunction(string linkPath, string targetPath)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = System.Diagnostics.Process.Start(startInfo)!;
        process.WaitForExit();
        if (process.ExitCode != 0 || !Directory.Exists(linkPath))
        {
            throw new InvalidOperationException($"Test fixture setup failed: could not create junction '{linkPath}' -> '{targetPath}'. stderr: {process.StandardError.ReadToEnd()}");
        }
    }
}
