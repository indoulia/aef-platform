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
//
// Story S25 traceability note (criterion -> proving test method(s)):
// this maps S25's acceptance criteria directly to the test methods that
// prove them, so a future reader does not have to re-derive coverage
// from scratch.
//
// AC1 - path-traversal attempt (".." , absolute path outside the root,
// or a symlink resolving outside the root) returns a structured failure
// and performs no read outside the boundary:
//   - ".." (single level, in-tree sibling)
//       -> DotDotTraversal_RefusesWithStructuredFailure_NeverThrowsNeverTruncates
//   - ".." walked past the filesystem's own drive root (S25 gap, closed here)
//       -> DeeplyNestedDotDotTraversal_PastTheFilesystemRootItself_RefusesWithStructuredFailure
//   - absolute path outside the root
//       -> AbsolutePathOutsideRoot_RefusesWithStructuredFailure
//       -> AbsolutePathOutsideRoot_RefusesWithStructuredFailure_EvenWhenAmbientCurrentDirectoryIsTheRoot
//   - symlink (NTFS junction) resolving outside the root, proving no
//     content read across the boundary (RepositoryEntry never carries
//     content, and the escaping call returns zero entries)
//       -> ReparsePointResolvingOutsideRoot_RefusesWithStructuredFailure_NeverFollowsTheLinkToListItsRealContent
//
// AC2 - the adapter's full public type surface has zero write- or
// process-execution-capable members, by static/reflective inspection:
//   - parameter types (S22)
//       -> Contract_HasNoNetworkCredentialOrProcessExecutionMember
//   - return types (S25 gap, closed here)
//       -> Contract_NoMemberReturnsAWriteOrProcessExecutionCapableType
//
// Related hardening (not one of the two numbered ACs above, but part of
// this Story's "structured failure, never an incidental exception"
// discipline; S25 gap, closed here): a null/empty root is a
// caller-contract violation rejected with a deliberate, documented
// ArgumentException rather than an unguarded framework exception from
// Path.GetFullPath.
//   -> NullOrEmptyRoot_ThrowsArgumentException_WithClearMessage
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

    // Story S25 gap: S22's reflection contract test above only inspected
    // parameter types. A member could just as easily be write- or
    // process-execution-capable through its *return* type (e.g. a
    // hypothetical method handing back a writable Stream/FileStream, or
    // a live Process). IRepositoryScanner's actual surface returns only
    // bool and RepositoryScanResult - both closed, inert data shapes -
    // but this asserts that mechanically, so a future member added to
    // the interface cannot silently reintroduce a write- or
    // execution-capable return type without failing this test.
    [Fact]
    public void Contract_NoMemberReturnsAWriteOrProcessExecutionCapableType()
    {
        var writeOrExecutionCapableKeywords = new[]
        {
            "stream", "writer", "process", "handle", "socket", "pipe",
        };

        var interfaceType = typeof(IRepositoryScanner);
        var returnTypes = interfaceType.GetMethods().Select(m => m.ReturnType).ToList();

        foreach (var returnType in returnTypes)
        {
            var typeName = returnType.Name.ToLowerInvariant();
            Assert.False(
                writeOrExecutionCapableKeywords.Any(keyword => typeName.Contains(keyword)),
                $"Return type '{returnType.Name}' looks write- or process-execution-capable, which IRepositoryScanner must never expose.");
        }

        // The only return shapes this contract may expose are closed,
        // inert data - never a live handle, stream, or process object
        // that could be written to or used to execute code.
        var allowedReturnTypes = new[] { typeof(bool), typeof(RepositoryScanResult) };
        Assert.All(returnTypes, t => Assert.Contains(t, allowedReturnTypes));
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

    // Guards against a specific regression: resolving relativePath against
    // root one path-segment at a time turns a genuine absolute path (e.g.
    // "C:\Windows\System32") into a bare drive-letter token ("C:") once
    // split into segments, and Path.GetFullPath("C:") resolves relative to
    // the *process's ambient current directory on that drive* - not the
    // caller's real absolute target. If Environment.CurrentDirectory
    // happens to equal the scanner's root, that bug silently reinterprets
    // an absolute out-of-root path as an in-root path. This test pins
    // CurrentDirectory to the fixture root before scanning, so it fails
    // under the buggy segment-by-segment resolution and only passes when
    // the absolute path is resolved via a single-shot Path.Combine(root,
    // relativePath), independent of ambient working directory.
    [Fact]
    public void AbsolutePathOutsideRoot_RefusesWithStructuredFailure_EvenWhenAmbientCurrentDirectoryIsTheRoot()
    {
        var outside = Directory.CreateTempSubdirectory("s22-outside-absolute-cwd-");
        var originalCurrentDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = _root.FullName;
            var scanner = new LocalFileSystemRepositoryScanner(_root.FullName);

            var result = scanner.ListEntries(outside.FullName);

            Assert.Equal(RepositoryScanStatus.PathEscapesRoot, result.Status);
            Assert.Empty(result.Entries);
            Assert.NotNull(result.FailureReason);
        }
        finally
        {
            Environment.CurrentDirectory = originalCurrentDirectory;
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

    // Story S25 gap: only a single ".." level was tested by S22. A path
    // that walks ".." far enough to run past the filesystem's own drive
    // root (e.g. "../../../../../../../../etc") does not throw on
    // Windows - Path.GetFullPath simply clamps at the drive root and
    // keeps resolving remaining segments from there - so this exercises
    // a distinct code path from a single in-tree ".." (which resolves to
    // a real sibling directory). This proves the escape is caught by the
    // deliberate root-boundary check (IsWithinRoot), not by an
    // accidental exception from an out-of-range path: GetFullPath never
    // throws for excessive ".." segments, so if this test passed only
    // because of the generic catch-and-fail-safe in ListEntries, that
    // would itself be a red flag worth knowing about, not a coincidence
    // to rely on.
    [Fact]
    public void DeeplyNestedDotDotTraversal_PastTheFilesystemRootItself_RefusesWithStructuredFailure()
    {
        var scanner = new LocalFileSystemRepositoryScanner(_root.FullName);
        var farPastRoot = string.Concat(Enumerable.Repeat(".." + Path.DirectorySeparatorChar, 20)) + "etc";

        var result = scanner.ListEntries(farPastRoot);

        Assert.Equal(RepositoryScanStatus.PathEscapesRoot, result.Status);
        Assert.Empty(result.Entries);
        Assert.NotNull(result.FailureReason);
    }

    // Story S25 gap: a null or empty root previously reached
    // Path.GetFullPath(root) unguarded, surfacing .NET's own
    // ArgumentNullException/ArgumentException with a generic framework
    // message. Since this Story's purpose is proving the isolation
    // boundary fails in a structured, deliberate way rather than by
    // incidental exception, the constructor now guards explicitly. This
    // is a caller-contract violation (an adapter cannot enforce a
    // boundary it was never given), not a runtime environmental failure
    // like a missing directory, so throwing (rather than a
    // RepositoryScanResult failure, which is reserved for failures
    // discoverable only once a root and a call are both in hand) is the
    // right shape here - but the exception and its message must be
    // deliberate and documented, not a default framework one.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullOrEmptyRoot_ThrowsArgumentException_WithClearMessage(string? root)
    {
        var exception = Assert.Throws<ArgumentException>(() => new LocalFileSystemRepositoryScanner(root!));

        Assert.Equal("root", exception.ParamName);
        Assert.Contains("must not be null or empty", exception.Message);
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
