using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace Adapters.Rest.Tests;

public class RequestObservabilityMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RequestObservabilityMiddlewareTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);
        public void Dispose() { }

        private sealed class CapturingLogger : ILogger
        {
            private readonly List<string> _messages;
            public CapturingLogger(List<string> messages) => _messages = messages;
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                _messages.Add(formatter(state, exception));
        }
    }

    [Fact]
    public async Task GetVersion_LogsRequestStartedAndCompleted()
    {
        var provider = new CapturingLoggerProvider();
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(provider));
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/version");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains(provider.Messages, m => m.Contains("Request started") && m.Contains("/api/v1/version"));
        Assert.Contains(provider.Messages, m => m.Contains("Request completed") && m.Contains("200"));
    }
}
