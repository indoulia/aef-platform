namespace Runtime.Ai.Prompts;

public sealed record PromptRenderResult
{
    public bool Success { get; }
    public string? RenderedUserPrompt { get; }
    public IReadOnlyList<string> MissingVariables { get; }

    private PromptRenderResult(bool success, string? renderedUserPrompt, IReadOnlyList<string> missingVariables)
    {
        Success = success;
        RenderedUserPrompt = renderedUserPrompt;
        MissingVariables = missingVariables;
    }

    public static PromptRenderResult Rendered(string text) => new(true, text, Array.Empty<string>());

    public static PromptRenderResult MissingVariablesResult(IReadOnlyList<string> missing) => new(false, null, missing);
}
