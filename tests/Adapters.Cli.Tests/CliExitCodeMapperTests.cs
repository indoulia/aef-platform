using Runtime.Agent;

namespace Adapters.Cli.Tests;

public class CliExitCodeMapperTests
{
    [Theory]
    [InlineData(ExecutionStatus.Completed, 0)]
    [InlineData(ExecutionStatus.NeedsClarification, 2)]
    [InlineData(ExecutionStatus.Failed, 1)]
    public void MapExecutionStatus_ReturnsDocumentedCode(ExecutionStatus status, int expectedExitCode)
    {
        Assert.Equal(expectedExitCode, CliExitCodeMapper.MapExecutionStatus(status));
    }

    [Fact]
    public void FixedExitCodes_MatchSysexits()
    {
        Assert.Equal(64, CliExitCodeMapper.UsageError);
        Assert.Equal(70, CliExitCodeMapper.SoftwareError);
        Assert.Equal(77, CliExitCodeMapper.SecurityDenied);
    }
}
