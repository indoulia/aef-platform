using Runtime.Ai;
using Xunit;

namespace Runtime.Ai.Tests;

public class AiPromptRequestTests
{
    [Fact]
    public void DefaultsAreZeroTemperatureAndTwoThousandTokens()
    {
        var request = new AiPromptRequest("PCT-EXE-0026", "system", "user");

        Assert.Equal(0.0, request.Temperature);
        Assert.Equal(2000, request.MaxOutputTokens);
    }
}
