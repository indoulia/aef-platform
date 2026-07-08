using System.Net;
using System.Text;
using Runtime.Ai;
using Runtime.Ai.Providers;
using Xunit;

namespace Runtime.Ai.Tests.Providers;

public class OpenAiProviderTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public HttpRequestMessage? LastRequest;
        public string? LastRequestBody;

        public FakeHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly string SuccessBody = """
    {
      "model": "gpt-4o",
      "choices": [ { "message": { "content": "{\"status\":\"ok\"}" } } ],
      "usage": { "prompt_tokens": 12, "completion_tokens": 4, "total_tokens": 16 }
    }
    """;

    [Fact]
    public async Task SendsBearerAuthAndReturnsParsedUsage()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, SuccessBody);
        var httpClient = new HttpClient(handler);
        var provider = new OpenAiProvider(httpClient, new OpenAiProviderOptions { ApiKey = "sk-test", Model = "gpt-4o" });

        var result = await provider.CompleteAsync(new AiPromptRequest("PCT-EXE-0026", "system", "user"), CancellationToken.None);

        Assert.Equal("{\"status\":\"ok\"}", result.RawText);
        Assert.Equal(16, result.Usage.TotalTokens);
        Assert.Equal("OpenAI", result.ProviderName);
        Assert.Equal("sk-test", handler.LastRequest!.Headers.Authorization!.Parameter);
        Assert.Contains("\"model\":\"gpt-4o\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task ClassifiesA429AsTransient()
    {
        var handler = new FakeHandler(HttpStatusCode.TooManyRequests, "{\"error\":\"rate limited\"}");
        var provider = new OpenAiProvider(new HttpClient(handler), new OpenAiProviderOptions { ApiKey = "sk-test", Model = "gpt-4o" });

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.CompleteAsync(new AiPromptRequest("PCT-EXE-0026", "system", "user"), CancellationToken.None));

        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task ClassifiesA401AsStructural()
    {
        var handler = new FakeHandler(HttpStatusCode.Unauthorized, "{\"error\":\"bad key\"}");
        var provider = new OpenAiProvider(new HttpClient(handler), new OpenAiProviderOptions { ApiKey = "sk-bad", Model = "gpt-4o" });

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.CompleteAsync(new AiPromptRequest("PCT-EXE-0026", "system", "user"), CancellationToken.None));

        Assert.False(ex.IsTransient);
    }

    private sealed class FailingReadContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new IOException("connection reset while streaming response body");

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class ResponseBodyReadFailsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new FailingReadContent() });
    }

    [Fact]
    public async Task ClassifiesAFailureReadingTheResponseBodyAsTransient()
    {
        var provider = new OpenAiProvider(new HttpClient(new ResponseBodyReadFailsHandler()), new OpenAiProviderOptions { ApiKey = "sk-test", Model = "gpt-4o" });

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.CompleteAsync(new AiPromptRequest("PCT-EXE-0026", "system", "user"), CancellationToken.None));

        Assert.True(ex.IsTransient);
    }
}
