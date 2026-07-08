namespace Runtime.Ai.Providers;

public sealed class OpenAiProviderOptions
{
    public required string ApiKey { get; init; }
    public required string Model { get; init; }
    public string BaseUrl { get; init; } = "https://api.openai.com/v1";
}
