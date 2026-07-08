using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Runtime.Ai.Providers;

public sealed class AzureOpenAiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly AzureOpenAiProviderOptions _options;

    public AzureOpenAiProvider(HttpClient httpClient, AzureOpenAiProviderOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public string Name => "AzureOpenAI";

    public async Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken)
    {
        var url = $"{_options.Endpoint.TrimEnd('/')}/openai/deployments/{_options.Deployment}/chat/completions?api-version={_options.ApiVersion}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Add("api-key", _options.ApiKey);
        httpRequest.Content = JsonContent.Create(new
        {
            temperature = request.Temperature,
            max_tokens = request.MaxOutputTokens,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt },
            },
        });

        HttpResponseMessage response;
        string body;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException($"Azure OpenAI request failed: {ex.Message}", isTransient: true, ex);
        }
        catch (IOException ex)
        {
            throw new AiProviderException($"Azure OpenAI response could not be read: {ex.Message}", isTransient: true, ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var isTransient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            throw new AiProviderException($"Azure OpenAI returned {(int)response.StatusCode}: {HttpErrorBodyFormatter.Truncate(body)}", isTransient);
        }

        AzureChatCompletionResponse parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AzureChatCompletionResponse>(body)
                ?? throw new AiProviderException("Azure OpenAI returned an empty response body.", isTransient: false);
        }
        catch (JsonException ex)
        {
            throw new AiProviderException($"Azure OpenAI returned an unparseable response: {ex.Message}", isTransient: false, ex);
        }

        var content = parsed.Choices.FirstOrDefault()?.Message?.Content
            ?? throw new AiProviderException("Azure OpenAI response carried no choices/message content.", isTransient: false);

        return new AiCompletionResult(
            content,
            new AiUsage(parsed.Usage?.PromptTokens ?? 0, parsed.Usage?.CompletionTokens ?? 0, parsed.Usage?.TotalTokens ?? 0),
            Name,
            parsed.Model ?? _options.Deployment);
    }

    private sealed record AzureChatCompletionResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] List<AzureChoice> Choices,
        [property: JsonPropertyName("usage")] AzureUsage? Usage);

    private sealed record AzureChoice([property: JsonPropertyName("message")] AzureMessage? Message);

    private sealed record AzureMessage([property: JsonPropertyName("content")] string? Content);

    private sealed record AzureUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens,
        [property: JsonPropertyName("total_tokens")] int TotalTokens);
}
