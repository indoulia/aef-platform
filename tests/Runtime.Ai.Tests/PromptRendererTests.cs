using Runtime.Ai.Prompts;
using Xunit;

namespace Runtime.Ai.Tests;

public class PromptRendererTests
{
    [Fact]
    public void SubstitutesAllPlaceholders()
    {
        var template = new PromptTemplate("PCT-EXE-0026", "system", "Review {{specification_instance}} at iteration {{review_iteration}}.");
        var variables = new Dictionary<string, object?> { ["specification_instance"] = "SPEC-1", ["review_iteration"] = 1 };

        var result = PromptRenderer.Render(template, variables);

        Assert.True(result.Success);
        Assert.Equal("Review SPEC-1 at iteration 1.", result.RenderedUserPrompt);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void ReportsMissingVariablesInsteadOfLeavingRawPlaceholders()
    {
        var template = new PromptTemplate("PCT-EXE-0026", "system", "Review {{specification_instance}} at iteration {{review_iteration}}.");
        var variables = new Dictionary<string, object?> { ["specification_instance"] = "SPEC-1" };

        var result = PromptRenderer.Render(template, variables);

        Assert.False(result.Success);
        Assert.Null(result.RenderedUserPrompt);
        Assert.Equal(new[] { "review_iteration" }, result.MissingVariables);
    }
}
