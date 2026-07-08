using Microsoft.Extensions.Options;
using Runtime.Agent.Health;

namespace AefPlatform.Host;

// GA-3 Capability 4's one real health contributor, per the Completion
// Gate's own example ("fails readiness if a dependency, e.g. the
// configured AI provider, is unreachable"). Checks network reachability
// only - any HTTP response (including 404/401) counts as Healthy, since
// this answers "can we reach the configured host," not "are our
// credentials valid." Reuses the same HttpClient singleton Program.cs
// already registers for IAiProvider - no new HTTP client, no new
// configuration surface.
public sealed class AiProviderReachabilityHealthContributor : IRuntimeHealthContributor
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<AiProviderOptions> _options;

    public AiProviderReachabilityHealthContributor(HttpClient httpClient, IOptions<AiProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public RuntimeHealthReport Check()
    {
        var options = _options.Value;
        var baseUrl = options.Provider == "AzureOpenAI"
            ? options.AzureOpenAi?.Endpoint
            : options.OpenAi?.BaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new RuntimeHealthReport("AiProviderReachability", RuntimeHealthStatus.Unhealthy, "No base URL configured for the selected AI provider.");
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var request = new HttpRequestMessage(HttpMethod.Head, baseUrl);
            using var response = _httpClient.Send(request, cts.Token);
            return new RuntimeHealthReport("AiProviderReachability", RuntimeHealthStatus.Healthy, null);
        }
        catch (Exception ex)
        {
            return new RuntimeHealthReport("AiProviderReachability", RuntimeHealthStatus.Unhealthy, $"AI provider endpoint unreachable: {ex.Message}");
        }
    }
}
