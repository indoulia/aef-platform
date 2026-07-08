namespace Runtime.Ai;

public sealed record AiCompletionResult(string RawText, AiUsage Usage, string ProviderName, string Model);
