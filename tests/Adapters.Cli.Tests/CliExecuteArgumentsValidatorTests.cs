namespace Adapters.Cli.Tests;

public class CliExecuteArgumentsValidatorTests
{
    [Fact]
    public void Validate_ValidArguments_ReturnsNull()
    {
        var args = new CliExecuteArguments("start a new feature", "default-profile", null, null, "text");

        Assert.Null(CliExecuteArgumentsValidator.Validate(args));
    }

    [Fact]
    public void Validate_EmptyUserRequest_ReturnsError()
    {
        var args = new CliExecuteArguments("", "default-profile", null, null, "text");

        Assert.NotNull(CliExecuteArgumentsValidator.Validate(args));
    }

    [Fact]
    public void Validate_EmptyProjectProfileId_ReturnsError()
    {
        var args = new CliExecuteArguments("start a new feature", "", null, null, "text");

        Assert.NotNull(CliExecuteArgumentsValidator.Validate(args));
    }

    [Fact]
    public void Validate_MalformedPayloadJson_ReturnsError()
    {
        var args = new CliExecuteArguments("start a new feature", "default-profile", null, "{ not valid json", "text");

        Assert.NotNull(CliExecuteArgumentsValidator.Validate(args));
    }

    [Fact]
    public void Validate_ValidPayloadJson_ReturnsNull()
    {
        var args = new CliExecuteArguments("start a new feature", "default-profile", null, "{\"key\":\"value\"}", "text");

        Assert.Null(CliExecuteArgumentsValidator.Validate(args));
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("JSON")]
    [InlineData("Text")]
    [InlineData("")]
    public void Validate_InvalidOutputValue_ReturnsError(string output)
    {
        var args = new CliExecuteArguments("start a new feature", "default-profile", null, null, output);

        Assert.NotNull(CliExecuteArgumentsValidator.Validate(args));
    }
}
