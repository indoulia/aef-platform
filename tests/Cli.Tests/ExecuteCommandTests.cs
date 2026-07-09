namespace Cli.Tests;

public class ExecuteCommandTests
{
    [Fact]
    public void Execute_DeterministicCapability_ExitsZeroAndPrintsCompleted()
    {
        var (exitCode, stdout, _) = CliExecutableFixture.Run("execute", "--user-request", "start a new feature", "--project-profile-id", "default-profile");

        Assert.Equal(0, exitCode);
        Assert.Contains("Completed", stdout);
    }

    [Fact]
    public void Execute_UnknownSessionId_ExitsOne()
    {
        var (exitCode, _, _) = CliExecutableFixture.Run("execute", "--user-request", "start a new feature", "--project-profile-id", "default-profile", "--session-id", "does-not-exist");

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Execute_EmptyUserRequest_ExitsWithUsageError()
    {
        var (exitCode, _, stderr) = CliExecutableFixture.Run("execute", "--user-request", "", "--project-profile-id", "default-profile");

        Assert.Equal(64, exitCode);
        Assert.NotEmpty(stderr);
    }

    [Fact]
    public void Execute_InvalidOutputValue_ExitsWithUsageError()
    {
        var (exitCode, _, stderr) = CliExecutableFixture.Run("execute", "--user-request", "x", "--project-profile-id", "default-profile", "--output", "xml");

        Assert.Equal(64, exitCode);
        Assert.NotEmpty(stderr);
    }

    [Fact]
    public void Execute_OutputValueWrongCase_ExitsWithUsageError()
    {
        var (exitCode, _, stderr) = CliExecutableFixture.Run("execute", "--user-request", "x", "--project-profile-id", "default-profile", "--output", "JSON");

        Assert.Equal(64, exitCode);
        Assert.NotEmpty(stderr);
    }
}
