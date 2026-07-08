using System.Net;
using System.Text;
using Runtime.Ai;
using Runtime.Ai.Providers;
using Xunit;

namespace Runtime.Ai.Tests.Providers;

public class AzureOpenAiProviderTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public HttpRequestMessage? LastRequest;

        public FakeHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") });
        }
    }

    private static readonly string SuccessBody = """
    {
      "model": "gpt-4o",
      "choices": [ { "message": { "content": "{\"status\":\"ok\"}" } } ],
      "usage": { "prompt_tokens": 20, "completion_tokens": 6, "total_tokens": 26 }
    }
    """;

    [Fact]
    public async Task PostsToTheDeploymentSpecificUrlWithApiKeyHeader()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, SuccessBody);
        var options = new AzureOpenAiProviderOptions
        {
            Endpoint = "https://example.openai.azure.com",
            ApiKey = "azure-key",
            Deployment = "gpt-4o-deployment",
        };
        var provider = new AzureOpenAiProvider(new HttpClient(handler), options);

        var result = await provider.CompleteAsync(new AiPromptRequest("PCT-EXE-0027", "system", "user"), CancellationToken.None);

        Assert.Equal("{\"status\":\"ok\"}", result.RawText);
        Assert.Equal(26, result.Usage.TotalTokens);
        Assert.Equal("AzureOpenAI", result.ProviderName);
        Assert.Equal(
            "https://example.openai.azure.com/openai/deployments/gpt-4o-deployment/chat/completions?api-version=2024-06-01",
            handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("azure-key", handler.LastRequest.Headers.GetValues("api-key").Single());
    }

    [Fact]
    public async Task ClassifiesA503AsTransient()
    {
        var handler = new FakeHandler(HttpStatusCode.ServiceUnavailable, "{\"error\":\"unavailable\"}");
        var provider = new AzureOpenAiProvider(new HttpClient(handler), new AzureOpenAiProviderOptions
        {
            Endpoint = "https://example.openai.azure.com",
            ApiKey = "azure-key",
            Deployment = "gpt-4o-deployment",
        });

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.CompleteAsync(new AiPromptRequest("PCT-EXE-0027", "system", "user"), CancellationToken.None));

        Assert.True(ex.IsTransient);
    }
}
