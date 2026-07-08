namespace Runtime.Ai;

public sealed record AiUsage(int PromptTokens, int CompletionTokens, int TotalTokens);
