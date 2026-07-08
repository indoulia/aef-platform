using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Runtime.Ai.Providers;

public sealed class OpenAiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiProviderOptions _options;

    public OpenAiProvider(HttpClient httpClient, OpenAiProviderOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public string Name => "OpenAI";

    public async Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Content = JsonContent.Create(new
        {
            model = _options.Model,
            temperature = request.Temperature,
            max_tokens = request.MaxOutputTokens,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt },
            },
        });

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException($"OpenAI request failed: {ex.Message}", isTransient: true, ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var isTransient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            throw new AiProviderException($"OpenAI returned {(int)response.StatusCode}: {body}", isTransient);
        }

        OpenAiChatCompletionResponse parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(body)
                ?? throw new AiProviderException("OpenAI returned an empty response body.", isTransient: false);
        }
        catch (JsonException ex)
        {
            throw new AiProviderException($"OpenAI returned an unparseable response: {ex.Message}", isTransient: false, ex);
        }

        var content = parsed.Choices.FirstOrDefault()?.Message?.Content
            ?? throw new AiProviderException("OpenAI response carried no choices/message content.", isTransient: false);

        return new AiCompletionResult(
            content,
            new AiUsage(parsed.Usage?.PromptTokens ?? 0, parsed.Usage?.CompletionTokens ?? 0, parsed.Usage?.TotalTokens ?? 0),
            Name,
            parsed.Model ?? _options.Model);
    }

    private sealed record OpenAiChatCompletionResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] List<OpenAiChoice> Choices,
        [property: JsonPropertyName("usage")] OpenAiUsage? Usage);

    private sealed record OpenAiChoice([property: JsonPropertyName("message")] OpenAiMessage? Message);

    private sealed record OpenAiMessage([property: JsonPropertyName("content")] string? Content);

    private sealed record OpenAiUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens,
        [property: JsonPropertyName("total_tokens")] int TotalTokens);
}
