// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
//
// Bound the same way PlatformHostOptions already is
// (.Bind().ValidateDataAnnotations().ValidateOnStart()) - Provider
// selects which of OpenAi/AzureOpenAi is actually constructed; the
// other section may be absent. Real API keys are never checked into
// appsettings.json - see the "Ai" section there for the placeholder
// values and the deploy-time environment-variable override convention.
using System.ComponentModel.DataAnnotations;

namespace AefPlatform.Host;

public sealed class AiProviderOptions
{
    [Required]
    [RegularExpression("OpenAI|AzureOpenAI|ClaudeCli")]
    public string Provider { get; set; } = "OpenAI";

    public int TimeoutSeconds { get; set; } = 60;

    public OpenAiSection? OpenAi { get; set; }

    public AzureOpenAiSection? AzureOpenAi { get; set; }

    public ClaudeCliSection ClaudeCli { get; set; } = new();

    // Uses the locally installed, signed-in claude CLI; no key or endpoint.
    public sealed class ClaudeCliSection
    {
        // The smallest model returned malformed JSON for a 9.6K-character review in real-provider dogfooding.
        public string Model { get; set; } = "sonnet";
    }

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
