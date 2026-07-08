using Runtime.Ai.Prompts;
using Xunit;

namespace Runtime.Ai.Tests.Prompts;

public class EngineeringPromptCatalogTests
{
    [Theory]
    [InlineData("PCT-EXE-0026")]
    [InlineData("PCT-EXE-0027")]
    [InlineData("PCT-EXE-0028")]
    [InlineData("PCT-EXE-0029")]
    public void EveryGovernedPromptContractIsPresent(string id)
    {
        var catalog = new EngineeringPromptCatalog();

        var template = catalog.Get(id);

        Assert.NotNull(template);
        Assert.Equal(id, template!.Id);
        Assert.False(string.IsNullOrWhiteSpace(template.SystemPrompt));
        Assert.False(string.IsNullOrWhiteSpace(template.UserPromptTemplate));
    }

    [Fact]
    public void ReturnsNullForAnUnknownId()
    {
        var catalog = new EngineeringPromptCatalog();

        Assert.Null(catalog.Get("PCT-EXE-9999"));
    }
}
