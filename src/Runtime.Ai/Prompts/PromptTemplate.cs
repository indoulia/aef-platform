namespace Runtime.Ai.Prompts;

public sealed record PromptTemplate(string Id, string SystemPrompt, string UserPromptTemplate);
