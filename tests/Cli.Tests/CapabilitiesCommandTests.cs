namespace Cli.Tests;

public class CapabilitiesCommandTests
{
    [Fact]
    public void Capabilities_ExitsZeroAndListsEightCapabilities_TextMode()
    {
        var (exitCode, stdout, _) = CliExecutableFixture.Run("capabilities");

        Assert.Equal(0, exitCode);
        Assert.Equal(8, stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void Capabilities_JsonMode_IsAnEightElementArray()
    {
        var (exitCode, stdout, _) = CliExecutableFixture.Run("capabilities", "--output", "json");

        Assert.Equal(0, exitCode);
        using var document = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal(8, document.RootElement.GetArrayLength());
    }

    [Fact]
    public void Capabilities_InvalidOutputValue_ExitsWithUsageError()
    {
        var (exitCode, _, stderr) = CliExecutableFixture.Run("capabilities", "--output", "xml");

        Assert.Equal(64, exitCode);
        Assert.NotEmpty(stderr);
    }
}
