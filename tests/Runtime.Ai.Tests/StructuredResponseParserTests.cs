using Runtime.Ai;
using Xunit;

namespace Runtime.Ai.Tests;

public class StructuredResponseParserTests
{
    private sealed record SampleOutput(string Status, int Score);

    [Fact]
    public void ParsesPlainJson()
    {
        var ok = StructuredResponseParser.TryParse<SampleOutput>("{\"Status\":\"ok\",\"Score\":7}", out var value, out var errors);

        Assert.True(ok);
        Assert.NotNull(value);
        Assert.Equal("ok", value!.Status);
        Assert.Equal(7, value.Score);
        Assert.Empty(errors);
    }

    [Fact]
    public void StripsMarkdownCodeFenceBeforeParsing()
    {
        var raw = "Here is the result:\n```json\n{\"Status\":\"ok\",\"Score\":9}\n```\n";

        var ok = StructuredResponseParser.TryParse<SampleOutput>(raw, out var value, out var errors);

        Assert.True(ok);
        Assert.Equal(9, value!.Score);
        Assert.Empty(errors);
    }

    [Fact]
    public void ReturnsErrorsInsteadOfThrowingOnMalformedJson()
    {
        var ok = StructuredResponseParser.TryParse<SampleOutput>("not json at all", out var value, out var errors);

        Assert.False(ok);
        Assert.Null(value);
        Assert.NotEmpty(errors);
    }
}
