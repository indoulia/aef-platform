namespace Adapters.Cli.Tests;

public class CliInstallArgumentsValidatorTests
{
    [Fact]
    public void Validate_ValidArguments_ReturnsNull()
    {
        var args = new CliInstallArguments("1.0.0", "C:/workspace/target");

        Assert.Null(CliInstallArgumentsValidator.Validate(args));
    }

    [Fact]
    public void Validate_EmptyVersion_ReturnsError()
    {
        var args = new CliInstallArguments("", "C:/workspace/target");

        Assert.NotNull(CliInstallArgumentsValidator.Validate(args));
    }

    [Fact]
    public void Validate_EmptyTargetPath_ReturnsError()
    {
        var args = new CliInstallArguments("1.0.0", "");

        Assert.NotNull(CliInstallArgumentsValidator.Validate(args));
    }
}
