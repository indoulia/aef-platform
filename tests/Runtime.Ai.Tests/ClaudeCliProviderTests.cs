using Runtime.Ai.Providers;
using Xunit;

namespace Runtime.Ai.Tests;

// The fixture is a verbatim `claude -p --output-format json` reply captured from the real CLI.
public class ClaudeCliProviderTests
{
    private static string Fixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory!.FullName, "AefPlatform.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(directory.FullName, "tests", "Runtime.Ai.Tests", "Fixtures", "claude_cli_result.json"));
    }

    private static readonly AiPromptRequest Request = new("PCT-TEST", "You are a reviewer.", "Review \"this\"\nplease.");

    private sealed class Recorder
    {
        public IReadOnlyList<string>? Arguments { get; private set; }
        public string? StandardInput { get; private set; }

        public ClaudeCliRunner Returning(int exitCode, string stdout, string stderr = "") => (arguments, stdin, _) =>
        {
            Arguments = arguments;
            StandardInput = stdin;
            return Task.FromResult(new ClaudeCliOutput(exitCode, stdout, stderr));
        };
    }

    [Fact]
    public async Task CompleteAsync_ParsesReplyTextAndUsageFromFixture()
    {
        var provider = new ClaudeCliProvider("haiku", new Recorder().Returning(0, Fixture()));

        var result = await provider.CompleteAsync(Request, CancellationToken.None);

        Assert.Equal("pong", result.RawText);
        Assert.Equal("ClaudeCli", result.ProviderName);
        Assert.Equal(4, result.Usage.CompletionTokens);
        Assert.True(result.Usage.PromptTokens > 0);
        Assert.Equal(result.Usage.PromptTokens + result.Usage.CompletionTokens, result.Usage.TotalTokens);
    }

    [Fact]
    public async Task CompleteAsync_SendsUserPromptOnStdin_AndRunsSlim()
    {
        var recorder = new Recorder();
        var provider = new ClaudeCliProvider("haiku", recorder.Returning(0, Fixture()));

        await provider.CompleteAsync(Request, CancellationToken.None);

        Assert.Equal("Review \"this\"\nplease.", recorder.StandardInput);
        var arguments = recorder.Arguments!.ToList();
        Assert.Equal("-p", arguments[0]);
        Assert.Equal("json", arguments[arguments.IndexOf("--output-format") + 1]);
        Assert.Equal("You are a reviewer.", arguments[arguments.IndexOf("--system-prompt") + 1]);
        Assert.Equal("haiku", arguments[arguments.IndexOf("--model") + 1]);
        Assert.Equal(string.Empty, arguments[arguments.IndexOf("--tools") + 1]);
        Assert.Contains("--strict-mcp-config", arguments);
        Assert.Contains("--no-session-persistence", arguments);
        Assert.DoesNotContain(arguments, argument => argument.Contains("Review"));
    }

    [Fact]
    public async Task CompleteAsync_NonZeroExit_ThrowsAiProviderException()
    {
        var provider = new ClaudeCliProvider("haiku", new Recorder().Returning(1, string.Empty, "not logged in"));

        var exception = await Assert.ThrowsAsync<AiProviderException>(() => provider.CompleteAsync(Request, CancellationToken.None));

        Assert.Contains("not logged in", exception.Message);
    }

    [Fact]
    public async Task CompleteAsync_ErrorResult_ThrowsAiProviderException()
    {
        var provider = new ClaudeCliProvider("haiku", new Recorder().Returning(0, "{\"type\":\"result\",\"is_error\":true,\"result\":\"Credit balance is too low\"}"));

        var exception = await Assert.ThrowsAsync<AiProviderException>(() => provider.CompleteAsync(Request, CancellationToken.None));

        Assert.Contains("Credit balance is too low", exception.Message);
    }

    [Fact]
    public async Task CompleteAsync_UnparseableOutput_ThrowsAiProviderException()
    {
        var provider = new ClaudeCliProvider("haiku", new Recorder().Returning(0, "not json"));

        await Assert.ThrowsAsync<AiProviderException>(() => provider.CompleteAsync(Request, CancellationToken.None));
    }

    [Fact]
    public async Task CompleteAsync_BinaryMissing_ThrowsAiProviderException()
    {
        ClaudeCliRunner missing = (_, _, _) => throw new System.ComponentModel.Win32Exception(2, "The system cannot find the file specified.");

        var exception = await Assert.ThrowsAsync<AiProviderException>(() => new ClaudeCliProvider("haiku", missing).CompleteAsync(Request, CancellationToken.None));

        Assert.Contains("claude", exception.Message);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task CompleteAsync_HonorsCancellation()
    {
        ClaudeCliRunner slow = async (_, _, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            return new ClaudeCliOutput(0, Fixture(), string.Empty);
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ClaudeCliProvider("haiku", slow).CompleteAsync(Request, cancellation.Token));
    }
}
