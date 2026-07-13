namespace Adapters.Cli.Tests;

public class FileSystemPlatformInstallerTests
{
    private static string CreateSourceDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"aef-platform-src-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "AefPlatform.Cli.dll"), "fake-binary-content");
        Directory.CreateDirectory(Path.Combine(dir, "runtimes"));
        File.WriteAllText(Path.Combine(dir, "runtimes", "win.dll"), "fake-native-content");
        return dir;
    }

    private static string TempTargetPath() =>
        Path.Combine(Path.GetTempPath(), $"aef-platform-target-{Guid.NewGuid():N}");

    [Fact]
    public void Install_RequestedVersionMatchesBuiltVersion_CopiesAllFiles_ReturnsSuccessWithTargetPath()
    {
        var source = CreateSourceDirectory();
        var target = TempTargetPath();
        var installer = new FileSystemPlatformInstaller(source, builtVersion: "1.0.0");

        try
        {
            var outcome = installer.Install(requestedVersion: "1.0.0", target);

            Assert.True(outcome.Succeeded);
            Assert.Equal(target, outcome.MaterializedPath);
            Assert.Null(outcome.FailureReason);
            Assert.True(File.Exists(Path.Combine(target, "AefPlatform.Cli.dll")));
            Assert.True(File.Exists(Path.Combine(target, "runtimes", "win.dll")));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        }
    }

    [Fact]
    public void Install_RequestedVersionDoesNotMatchBuiltVersion_ReturnsFailure_NoFilesCopied()
    {
        var source = CreateSourceDirectory();
        var target = TempTargetPath();
        var installer = new FileSystemPlatformInstaller(source, builtVersion: "1.0.0");

        try
        {
            var outcome = installer.Install(requestedVersion: "2.0.0", target);

            Assert.False(outcome.Succeeded);
            Assert.Null(outcome.MaterializedPath);
            Assert.Contains("2.0.0", outcome.FailureReason);
            Assert.Contains("1.0.0", outcome.FailureReason);
            Assert.False(Directory.Exists(target));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    [Fact]
    public void Install_RunTwice_SameVersionAndTarget_IsIdempotent()
    {
        var source = CreateSourceDirectory();
        var target = TempTargetPath();
        var installer = new FileSystemPlatformInstaller(source, builtVersion: "1.0.0");

        try
        {
            var first = installer.Install("1.0.0", target);
            var second = installer.Install("1.0.0", target);

            Assert.True(first.Succeeded);
            Assert.True(second.Succeeded);
            Assert.Equal(
                Directory.GetFiles(source, "*", SearchOption.AllDirectories).Length,
                Directory.GetFiles(target, "*", SearchOption.AllDirectories).Length);
        }
        finally
        {
            Directory.Delete(source, recursive: true);
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        }
    }

    [Fact]
    public void Install_SourceDirectoryMissing_ReturnsFailure_NeverThrows()
    {
        var missingSource = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}");
        var target = TempTargetPath();
        var installer = new FileSystemPlatformInstaller(missingSource, builtVersion: "1.0.0");

        var outcome = installer.Install("1.0.0", target);

        Assert.False(outcome.Succeeded);
        Assert.NotNull(outcome.FailureReason);
    }

    [Fact]
    public void Install_TwoDifferentTargetPaths_AreIndependent()
    {
        var source = CreateSourceDirectory();
        var targetA = TempTargetPath();
        var targetB = TempTargetPath();
        var installer = new FileSystemPlatformInstaller(source, builtVersion: "1.0.0");

        try
        {
            installer.Install("1.0.0", targetA);
            installer.Install("1.0.0", targetB);

            Assert.True(File.Exists(Path.Combine(targetA, "AefPlatform.Cli.dll")));
            Assert.True(File.Exists(Path.Combine(targetB, "AefPlatform.Cli.dll")));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
            if (Directory.Exists(targetA)) Directory.Delete(targetA, recursive: true);
            if (Directory.Exists(targetB)) Directory.Delete(targetB, recursive: true);
        }
    }
}
