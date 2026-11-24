using Runtime.DomainContent.RepositoryValidation;

namespace Runtime.Tests.DomainContent.RepositoryValidation;

public class BuildSystemDetectorTests
{
    [Fact]
    public void DetectDefaultValidationCommand_Dotnet_ReturnsDotnetTest()
    {
        var result = BuildSystemDetector.DetectDefaultValidationCommand(new[] { "dotnet" });

        Assert.Equal("dotnet", result!.Value.Command);
        Assert.Equal(new[] { "test" }, result.Value.Arguments);
    }

    [Fact]
    public void DetectDefaultValidationCommand_Node_ReturnsNpmTest()
    {
        var result = BuildSystemDetector.DetectDefaultValidationCommand(new[] { "node" });

        Assert.Equal("npm", result!.Value.Command);
        Assert.Equal(new[] { "test" }, result.Value.Arguments);
    }

    [Fact]
    public void DetectDefaultValidationCommand_Gradle_ReturnsTheWrapperTest()
    {
        var command = BuildSystemDetector.DetectDefaultValidationCommand(new[] { "gradle" });

        Assert.Equal("gradlew", command!.Value.Command);
        Assert.Equal(new[] { "test" }, command.Value.Arguments);
    }

    [Fact]
    public void DetectDefaultValidationCommand_UnknownStack_ReturnsNull()
    {
        var result = BuildSystemDetector.DetectDefaultValidationCommand(new[] { "unknown-stack" });

        Assert.Null(result);
    }

    [Fact]
    public void DetectDefaultValidationCommand_EmptyList_ReturnsNull()
    {
        var result = BuildSystemDetector.DetectDefaultValidationCommand(Array.Empty<string>());

        Assert.Null(result);
    }
}
