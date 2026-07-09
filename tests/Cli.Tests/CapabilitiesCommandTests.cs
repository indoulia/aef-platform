namespace Cli.Tests;

public class CapabilitiesCommandTests
{
    [Fact]
    public void Capabilities_ExitsZeroAndListsTenCapabilities_TextMode()
    {
        var (exitCode, stdout, _) = CliExecutableFixture.Run("capabilities");

        Assert.Equal(0, exitCode);
        Assert.Equal(10, stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void Capabilities_JsonMode_IsATenElementArray()
    {
        var (exitCode, stdout, _) = CliExecutableFixture.Run("capabilities", "--output", "json");

        Assert.Equal(0, exitCode);
        using var document = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal(10, document.RootElement.GetArrayLength());
    }

    [Fact]
    public void Capabilities_InvalidOutputValue_ExitsWithUsageError()
    {
        var (exitCode, _, stderr) = CliExecutableFixture.Run("capabilities", "--output", "xml");

        Assert.Equal(64, exitCode);
        Assert.NotEmpty(stderr);
    }
}
