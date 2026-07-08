namespace Runtime.Ai.Providers;

public sealed class AzureOpenAiProviderOptions
{
    public required string Endpoint { get; init; }
    public required string ApiKey { get; init; }
    public required string Deployment { get; init; }
    public string ApiVersion { get; init; } = "2024-06-01";
}
