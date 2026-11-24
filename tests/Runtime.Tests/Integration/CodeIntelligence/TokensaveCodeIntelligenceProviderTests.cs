using Runtime.Integration.CodeIntelligence;

namespace Runtime.Tests.Integration.CodeIntelligence;

// Fixtures are verbatim `tokensave tool <name> --json` outputs captured from this repository's own index.
public class TokensaveCodeIntelligenceProviderTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory!.FullName, "AefPlatform.sln")))
        {
            directory = directory.Parent;
        }

        return directory.FullName;
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "tests", "Runtime.Tests", "Integration", "CodeIntelligence", "Fixtures", name));

    private sealed class RecordingRunner
    {
        public List<IReadOnlyList<string>> Calls { get; } = new();
        public ProcessOutput Output { get; set; } = new(0, string.Empty, string.Empty, false, false);

        public ProcessOutput Run(string workingDirectory, string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            Calls.Add(arguments);
            return Output;
        }
    }

    private static (TokensaveCodeIntelligenceProvider Provider, RecordingRunner Runner) Create(ProcessOutput output)
    {
        var runner = new RecordingRunner { Output = output };
        return (new TokensaveCodeIntelligenceProvider(runner.Run), runner);
    }

    private static ProcessOutput Ok(string stdout) => new(0, stdout, string.Empty, false, false);

    [Fact]
    public void FindSymbol_ParsesSearchFixture_ReturnsReferencesWithFileAndLine()
    {
        var (provider, _) = Create(Ok(Fixture("search.json")));

        var result = provider.FindSymbol("C:/repo", "McpToolInvoker", 10);

        Assert.True(result.Available);
        var first = result.References[0];
        Assert.Equal("McpToolInvoker", first.Name);
        Assert.Equal("class", first.Kind);
        Assert.Equal("src/Adapters.Mcp/McpToolInvoker.cs", first.File);
        Assert.Equal(25, first.Line);
        Assert.Equal("tokensave", first.Source);
    }

    [Fact]
    public void FindSymbol_RespectsMaxResults()
    {
        var (provider, _) = Create(Ok(Fixture("search.json")));

        Assert.Equal(2, provider.FindSymbol("C:/repo", "McpToolInvoker", 2).References.Count);
    }

    [Fact]
    public void FindSymbol_NoMatch_IsAvailableWithNoReferences()
    {
        var (provider, _) = Create(Ok(Fixture("search_empty.json")));

        var result = provider.FindSymbol("C:/repo", "ZzzNoSuchSymbolQqq", 10);

        Assert.True(result.Available);
        Assert.Empty(result.References);
    }

    [Fact]
    public void FindRelevant_ParsesContextFixture_ReturnsEntryPointsWithFileAndLine()
    {
        var (provider, _) = Create(Ok(Fixture("context.json")));

        var result = provider.FindRelevant("C:/repo", "where are MCP tool calls translated", 10);

        Assert.True(result.Available);
        Assert.Contains(result.References, r => r.Name == "Tools" && r.File == "src/Adapters.Mcp/McpInfrastructureTools.cs" && r.Line == 19);
        Assert.Contains(result.References, r => r.Name == "BuildExecutionRequest" && r.File == "src/Adapters.Mcp/McpToolInvoker.cs" && r.Line == 57);
        Assert.All(result.References, r => Assert.False(string.IsNullOrEmpty(r.File)));
    }

    [Fact]
    public void FindTests_ParsesTestMapFixture_ReturnsTestFiles()
    {
        var (provider, _) = Create(Ok(Fixture("test_map.json")));

        var result = provider.FindTests("C:/repo", "src/Adapters.Mcp/McpToolInvoker.cs");

        Assert.Equal("tests/Adapters.Mcp.Tests/McpToolInvokerTests.cs", Assert.Single(result.References).File);
    }

    [Fact]
    public void FindAffected_ParsesAffectedFixture_ReturnsAffectedTestFiles()
    {
        var (provider, _) = Create(Ok(Fixture("affected.json")));

        var result = provider.FindAffected("C:/repo", new[] { "src/Adapters.Mcp/McpToolInvoker.cs" });

        Assert.Equal("tests/Adapters.Mcp.Tests/McpToolInvokerTests.cs", Assert.Single(result.References).File);
    }

    [Fact]
    public void AnyOperation_ToolNotFound_ReturnsUnavailable_NamingTokensave()
    {
        var (provider, _) = Create(new ProcessOutput(-1, string.Empty, string.Empty, false, NotFound: true));

        var result = provider.FindSymbol("C:/repo", "X", 5);

        Assert.False(result.Available);
        Assert.Contains("tokensave", result.UnavailableReason);
        Assert.Empty(result.References);
    }

    [Fact]
    public void AnyOperation_NotIndexed_ReturnsUnavailable_TellingCallerToRunTokensaveInit()
    {
        var (provider, _) = Create(new ProcessOutput(1, string.Empty, "Error: config error: no TokenSave index found at 'C:\\repo' — run 'tokensave init' first", false, false));

        var result = provider.FindRelevant("C:/repo", "anything", 5);

        Assert.False(result.Available);
        Assert.Contains("tokensave init", result.UnavailableReason);
    }

    [Fact]
    public void AnyOperation_TimedOut_ReturnsUnavailable()
    {
        var (provider, _) = Create(new ProcessOutput(-1, string.Empty, string.Empty, TimedOut: true, false));

        var result = provider.FindTests("C:/repo", "a.cs");

        Assert.False(result.Available);
        Assert.Contains("timed out", result.UnavailableReason);
    }

    [Fact]
    public void AnyOperation_UnparseableOutput_ReturnsUnavailable_NeverThrows()
    {
        var (provider, _) = Create(Ok("this is not json"));

        var result = provider.FindSymbol("C:/repo", "X", 5);

        Assert.False(result.Available);
    }

    [Fact]
    public void FindSymbol_NormalizesBackslashesToForwardSlashes()
    {
        var inner = "[{\"name\":\"A\",\"kind\":\"class\",\"file\":\"src\\\\Dir\\\\A.cs\",\"line\":3,\"signature\":null}]";
        var envelope = System.Text.Json.JsonSerializer.Serialize(new { content = new[] { new { type = "text", text = inner } } });
        var (provider, _) = Create(Ok(envelope));

        Assert.Equal("src/Dir/A.cs", provider.FindSymbol("C:/repo", "A", 5).References[0].File);
    }

    [Fact]
    public void FindRelevant_PassesTaskDescriptionWithQuotesAndNewlines_AsOneArgument()
    {
        var (provider, runner) = Create(Ok(Fixture("context.json")));
        const string task = "fix the \"retry\" bug\nin `Submit`";

        provider.FindRelevant("C:/my repo", task, 5);

        var arguments = Assert.Single(runner.Calls);
        Assert.Equal(new[] { "tool", "context", "--project", "C:/my repo", "--json", "--args" }, arguments.Take(6));
        using var args = System.Text.Json.JsonDocument.Parse(arguments[6]);
        Assert.Equal(task, args.RootElement.GetProperty("task").GetString());
        Assert.Equal(7, arguments.Count);
    }

    [Fact]
    public void FindSymbol_AgainstThisRepository_FindsEngineeringExecutionPipeline()
    {
        var provider = new TokensaveCodeIntelligenceProvider();

        var result = provider.FindSymbol(RepositoryRoot(), "EngineeringExecutionPipeline", 5);

        if (!result.Available)
        {
            return; // tokensave absent or this checkout not indexed: nothing to assert against
        }

        Assert.Contains(result.References, r => r.File == "src/Runtime/Execution/EngineeringExecutionPipeline.cs");
    }
}
