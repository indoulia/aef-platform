using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace Runtime.Ai.Providers;

public sealed record ClaudeCliOutput(int ExitCode, string StandardOutput, string StandardError);

public delegate Task<ClaudeCliOutput> ClaudeCliRunner(IReadOnlyList<string> arguments, string standardInput, CancellationToken cancellationToken);

// Runs a prompt through the locally installed, already signed-in `claude` CLI - no API key needed.
// The call is deliberately slim: own system prompt, no tools, no MCP servers, no saved session. A default
// headless call loads the whole Claude Code context (~48K tokens) for every request.
public sealed class ClaudeCliProvider : IAiProvider
{
    private readonly string _model;
    private readonly ClaudeCliRunner _runner;

    public ClaudeCliProvider(string model)
        : this(model, RunClaudeAsync)
    {
    }

    public ClaudeCliProvider(string model, ClaudeCliRunner runner)
    {
        _model = model;
        _runner = runner;
    }

    public string Name => "ClaudeCli";

    public async Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken)
    {
        var arguments = new[]
        {
            "-p", "--output-format", "json",
            "--system-prompt", request.SystemPrompt,
            "--model", _model,
            "--tools", string.Empty,
            "--strict-mcp-config",
            "--no-session-persistence",
        };

        ClaudeCliOutput output;
        try
        {
            output = await _runner(arguments, request.UserPrompt, cancellationToken);
        }
        catch (Win32Exception ex)
        {
            throw new AiProviderException($"The claude CLI could not be started - is it installed and on PATH? {ex.Message}", isTransient: false, ex);
        }

        if (output.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(output.StandardError) ? output.StandardOutput : output.StandardError;
            throw new AiProviderException($"The claude CLI exited with code {output.ExitCode}: {HttpErrorBodyFormatter.Truncate(detail.Trim())}", isTransient: false);
        }

        try
        {
            using var document = JsonDocument.Parse(output.StandardOutput.TrimStart('﻿'));
            var root = document.RootElement;
            var text = root.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.String ? result.GetString() : null;

            if (root.TryGetProperty("is_error", out var isError) && isError.ValueKind == JsonValueKind.True)
            {
                throw new AiProviderException($"The claude CLI reported an error: {text ?? "no detail given"}", isTransient: false);
            }

            if (text is null)
            {
                throw new AiProviderException("The claude CLI reply carried no result text.", isTransient: false);
            }

            var promptTokens = 0;
            var completionTokens = 0;
            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                promptTokens = Number(usage, "input_tokens") + Number(usage, "cache_creation_input_tokens") + Number(usage, "cache_read_input_tokens");
                completionTokens = Number(usage, "output_tokens");
            }

            return new AiCompletionResult(text, new AiUsage(promptTokens, completionTokens, promptTokens + completionTokens), Name, _model);
        }
        catch (JsonException ex)
        {
            throw new AiProviderException($"The claude CLI returned an unparseable reply: {ex.Message}", isTransient: false, ex);
        }
    }

    private static int Number(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;

    private static async Task<ClaudeCliOutput> RunClaudeAsync(IReadOnlyList<string> arguments, string standardInput, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("claude")
        {
            // A neutral directory: the CLI must not pick up a project's CLAUDE.md or settings.
            WorkingDirectory = Path.GetTempPath(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new Win32Exception(2, "Process.Start returned no process.");
        try
        {
            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellationToken);
            return new ClaudeCliOutput(process.ExitCode, await standardOutput, await standardError);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort: it may already have exited.
            }

            throw;
        }
    }
}
