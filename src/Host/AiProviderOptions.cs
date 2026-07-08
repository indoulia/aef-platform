using System.ComponentModel.DataAnnotations;

namespace AefPlatform.Host;

// Bound the same way PlatformHostOptions already is
// (.Bind().ValidateDataAnnotations().ValidateOnStart()) - Provider
// selects which of OpenAi/AzureOpenAi is actually constructed; the
// other section may be absent. Real API keys are never checked into
// appsettings.json - see the "Ai" section there for the placeholder
// values and the deploy-time environment-variable override convention.
public sealed class AiProviderOptions
{
    [Required]
    [RegularExpression("OpenAI|AzureOpenAI")]
    public string Provider { get; set; } = "OpenAI";

    public int TimeoutSeconds { get; set; } = 60;

    public OpenAiSection? OpenAi { get; set; }

    public AzureOpenAiSection? AzureOpenAi { get; set; }

    public sealed class OpenAiSection
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-4o";
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    }

    public sealed class AzureOpenAiSection
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Deployment { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = "2024-06-01";
    }
}
