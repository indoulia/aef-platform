# GA-3 Capability 4: Cross-Cutting Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement GA-3 Capability 4 per `docs/superpowers/specs/2026-07-08-ga3-capability4-cross-cutting-hardening-design.md`: real health-contributor logic behind REST's `/api/v1/health` placeholder, structured per-request logging, BCL-only request metrics, and a configured graceful-shutdown timeout.

**Architecture:** `Runtime.Agent.Health` (additive contracts) + one real contributor (`AiProviderReachabilityHealthContributor`, `Host`) replace Capability 2's fixed health response. `Adapters.Rest` gains a logging/metrics middleware. `RestAdapterOptions`/`Program.cs` gain a `HostOptions.ShutdownTimeout` configuration.

**Tech Stack:** .NET 8, `System.Diagnostics.Metrics` (BCL, no new package), `System.Net.HttpListener` (BCL, test-only, for network-independent reachability tests).

## Global Constraints

- No new NuGet package dependency anywhere.
- No CLI logging/observability changes — Capability 3's design already settled CLI as log-free.
- No existing GA-1/GA-2/Capability-1/-2/-3 contract change — only Capability 2's `Health` handler body changes (its route and response shape are unchanged), exactly as Capability 1's and Capability 2's own designs anticipated.
- **No test may depend on real internet connectivity.** Endpoint-level health tests use DI-substituted fake contributors (`services.RemoveAll<IRuntimeHealthContributor>()` before adding a fake — plain `AddSingleton` would only *add* to the `IEnumerable<IRuntimeHealthContributor>` the real, network-calling contributor already populates, silently making the test flaky under sandboxed/offline CI). The contributor's own unit tests use a local, in-test `HttpListener` on loopback, never a real external host.
- The full existing test suite (441 tests through Capability 3) must stay green throughout — re-run as the final gate.

---

### Task 1: Health-contributor contracts (`Runtime.Agent.Health`)

**Files:**
- Create: `src/Runtime.Agent/Health/RuntimeHealthStatus.cs`, `RuntimeHealthReport.cs`, `IRuntimeHealthContributor.cs`, `RuntimeHealthAggregator.cs`
- Test: `tests/Runtime.Agent.Tests/Health/RuntimeHealthAggregatorTests.cs` (create)

**Interfaces:**
- Produces: `Runtime.Agent.Health.RuntimeHealthStatus { Healthy, Degraded, Unhealthy }`, `RuntimeHealthReport(string Name, RuntimeHealthStatus Status, string? Reason)`, `IRuntimeHealthContributor { RuntimeHealthReport Check(); }`, `RuntimeHealthAggregator.Aggregate(IEnumerable<IRuntimeHealthContributor>) : RuntimeHealthReport` — consumed by Task 2 (contributor) and Task 3 (REST endpoint).

- [ ] **Step 1: Write the failing test**

Create `tests/Runtime.Agent.Tests/Health/RuntimeHealthAggregatorTests.cs`:

```csharp
using Runtime.Agent.Health;

namespace Runtime.Agent.Tests.Health;

public class RuntimeHealthAggregatorTests
{
    private sealed class FixedContributor : IRuntimeHealthContributor
    {
        private readonly RuntimeHealthReport _report;
        public FixedContributor(RuntimeHealthReport report) => _report = report;
        public RuntimeHealthReport Check() => _report;
    }

    [Fact]
    public void Aggregate_NoContributors_ReturnsHealthy()
    {
        var result = RuntimeHealthAggregator.Aggregate(Array.Empty<IRuntimeHealthContributor>());

        Assert.Equal(RuntimeHealthStatus.Healthy, result.Status);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Aggregate_AllHealthy_ReturnsHealthy()
    {
        var contributors = new[]
        {
            new FixedContributor(new RuntimeHealthReport("A", RuntimeHealthStatus.Healthy, null)),
            new FixedContributor(new RuntimeHealthReport("B", RuntimeHealthStatus.Healthy, null)),
        };

        var result = RuntimeHealthAggregator.Aggregate(contributors);

        Assert.Equal(RuntimeHealthStatus.Healthy, result.Status);
    }

    [Fact]
    public void Aggregate_OneUnhealthy_ReturnsUnhealthyWithReasonNamingIt()
    {
        var contributors = new[]
        {
            new FixedContributor(new RuntimeHealthReport("A", RuntimeHealthStatus.Healthy, null)),
            new FixedContributor(new RuntimeHealthReport("B", RuntimeHealthStatus.Unhealthy, "down")),
        };

        var result = RuntimeHealthAggregator.Aggregate(contributors);

        Assert.Equal(RuntimeHealthStatus.Unhealthy, result.Status);
        Assert.Contains("B: down", result.Reason);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails to compile**

Run: `dotnet test tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj --filter "FullyQualifiedName~RuntimeHealthAggregatorTests"`
Expected: build FAILS — the `Runtime.Agent.Health` namespace/types don't exist yet.

- [ ] **Step 3: Implement the contracts**

Create `src/Runtime.Agent/Health/RuntimeHealthStatus.cs`:

```csharp
namespace Runtime.Agent.Health;

// Declaration order is load-bearing: RuntimeHealthAggregator.Aggregate
// uses the default enum ordinal ordering to pick the worst status via
// Enumerable.Max. Reordering these members changes behavior, not just
// cosmetics.
public enum RuntimeHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
}
```

Create `src/Runtime.Agent/Health/RuntimeHealthReport.cs`:

```csharp
namespace Runtime.Agent.Health;

public sealed record RuntimeHealthReport(string Name, RuntimeHealthStatus Status, string? Reason);
```

Create `src/Runtime.Agent/Health/IRuntimeHealthContributor.cs`:

```csharp
namespace Runtime.Agent.Health;

public interface IRuntimeHealthContributor
{
    RuntimeHealthReport Check();
}
```

Create `src/Runtime.Agent/Health/RuntimeHealthAggregator.cs`:

```csharp
namespace Runtime.Agent.Health;

public static class RuntimeHealthAggregator
{
    public static RuntimeHealthReport Aggregate(IEnumerable<IRuntimeHealthContributor> contributors)
    {
        var reports = contributors.Select(c => c.Check()).ToList();
        if (reports.Count == 0)
        {
            return new RuntimeHealthReport("Aggregate", RuntimeHealthStatus.Healthy, null);
        }

        var worstStatus = reports.Max(r => r.Status);
        var reasons = reports.Where(r => r.Status != RuntimeHealthStatus.Healthy).Select(r => $"{r.Name}: {r.Reason}").ToList();
        return new RuntimeHealthReport("Aggregate", worstStatus, reasons.Count > 0 ? string.Join("; ", reasons) : null);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj --filter "FullyQualifiedName~RuntimeHealthAggregatorTests"`
Expected: all 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Runtime.Agent/Health tests/Runtime.Agent.Tests/Health
git commit -m "feat(runtime.agent): add the health-contributor seam (Runtime.Agent.Health)"
```

---

### Task 2: `AiProviderReachabilityHealthContributor`

**Files:**
- Create: `src/Host/AiProviderReachabilityHealthContributor.cs`
- Test: `tests/Host.Tests/AiProviderReachabilityHealthContributorTests.cs` (create)

**Interfaces:**
- Consumes: `Runtime.Agent.Health.IRuntimeHealthContributor`/`RuntimeHealthReport`/`RuntimeHealthStatus` (Task 1), `AefPlatform.Host.AiProviderOptions` (Composition).
- Produces: `AefPlatform.Host.AiProviderReachabilityHealthContributor : IRuntimeHealthContributor` — consumed by Task 3's `Program.cs` registration.

- [ ] **Step 1: Write the failing tests (network-independent — local `HttpListener` only)**

Create `tests/Host.Tests/AiProviderReachabilityHealthContributorTests.cs`:

```csharp
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace Host.Tests;

public class AiProviderReachabilityHealthContributorTests
{
    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public void Check_ReachableUrl_ReturnsHealthy()
    {
        var port = GetFreeLoopbackPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var serverTask = Task.Run(() =>
        {
            var context = listener.GetContext();
            context.Response.StatusCode = 200;
            context.Response.OutputStream.Close();
        });

        var options = Options.Create(new AiProviderOptions
        {
            Provider = "OpenAI",
            OpenAi = new AiProviderOptions.OpenAiSection { BaseUrl = $"http://127.0.0.1:{port}/" },
        });
        var contributor = new AiProviderReachabilityHealthContributor(new HttpClient(), options);

        var report = contributor.Check();

        Assert.Equal(RuntimeHealthStatus.Healthy, report.Status);
        serverTask.Wait(TimeSpan.FromSeconds(2));
        listener.Stop();
    }

    [Fact]
    public void Check_UnreachablePort_ReturnsUnhealthy()
    {
        var options = Options.Create(new AiProviderOptions
        {
            Provider = "OpenAI",
            OpenAi = new AiProviderOptions.OpenAiSection { BaseUrl = "http://127.0.0.1:1/" },
        });
        var contributor = new AiProviderReachabilityHealthContributor(new HttpClient(), options);

        var report = contributor.Check();

        Assert.Equal(RuntimeHealthStatus.Unhealthy, report.Status);
    }

    [Fact]
    public void Check_NoBaseUrlConfigured_ReturnsUnhealthy()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "OpenAI", OpenAi = null });
        var contributor = new AiProviderReachabilityHealthContributor(new HttpClient(), options);

        var report = contributor.Check();

        Assert.Equal(RuntimeHealthStatus.Unhealthy, report.Status);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj --filter "FullyQualifiedName~AiProviderReachabilityHealthContributorTests"`
Expected: build FAILS — `AiProviderReachabilityHealthContributor` does not exist.

- [ ] **Step 3: Implement the contributor**

Create `src/Host/AiProviderReachabilityHealthContributor.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj --filter "FullyQualifiedName~AiProviderReachabilityHealthContributorTests"`
Expected: all 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Host/AiProviderReachabilityHealthContributor.cs tests/Host.Tests/AiProviderReachabilityHealthContributorTests.cs
git commit -m "feat(host): add the AI-provider-reachability health contributor"
```

---

### Task 3: Wire the REST health endpoint to real aggregation

**Files:**
- Modify: `src/Adapters.Rest/RestAdapterEndpointExtensions.cs`
- Modify: `src/Host/Program.cs`
- Modify: `tests/Adapters.Rest.Tests/HealthEndpointTests.cs`

**Interfaces:**
- Consumes: `RuntimeHealthAggregator.Aggregate`, `IRuntimeHealthContributor` (Task 1), `AiProviderReachabilityHealthContributor` (Task 2).
- Produces: `GET /api/v1/health` returning `200 OK` (`Healthy`/`Degraded`) or `503 Service Unavailable` (`Unhealthy`).

- [ ] **Step 1: Write the failing tests (DI-substituted fakes — no real network call)**

Replace the full contents of `tests/Adapters.Rest.Tests/HealthEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runtime.Agent.Health;

namespace Adapters.Rest.Tests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private sealed class FakeContributor : IRuntimeHealthContributor
    {
        private readonly RuntimeHealthReport _report;
        public FakeContributor(RuntimeHealthReport report) => _report = report;
        public RuntimeHealthReport Check() => _report;
    }

    // RemoveAll is required, not optional: IEnumerable<IRuntimeHealthContributor>
    // resolves every registration, so a plain AddSingleton would run
    // Program.cs's real, network-calling AiProviderReachabilityHealthContributor
    // *alongside* the fake, reintroducing exactly the internet dependency
    // this test suite must not have.
    private WebApplicationFactory<Program> FactoryWithContributor(RuntimeHealthReport report) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRuntimeHealthContributor>();
                services.AddSingleton<IRuntimeHealthContributor>(new FakeContributor(report));
            });
        });

    [Fact]
    public async Task GetHealth_AllContributorsHealthy_ReturnsOk()
    {
        using var factory = FactoryWithContributor(new RuntimeHealthReport("Fake", RuntimeHealthStatus.Healthy, null));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RestHealthResponse>();
        Assert.Equal("Healthy", body!.Status);
    }

    [Fact]
    public async Task GetHealth_AContributorIsUnhealthy_ReturnsServiceUnavailable()
    {
        using var factory = FactoryWithContributor(new RuntimeHealthReport("Fake", RuntimeHealthStatus.Unhealthy, "Simulated failure."));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RestHealthResponse>();
        Assert.Equal("Unhealthy", body!.Status);
    }

    [Fact]
    public async Task GetHealth_ADegradedContributor_StillReturnsOk()
    {
        using var factory = FactoryWithContributor(new RuntimeHealthReport("Fake", RuntimeHealthStatus.Degraded, "Simulated degradation."));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RestHealthResponse>();
        Assert.Equal("Degraded", body!.Status);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail (the current handler always returns `"Healthy"`/`200`)**

Run: `dotnet test tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj --filter "FullyQualifiedName~HealthEndpointTests"`
Expected: `GetHealth_AllContributorsHealthy_ReturnsOk` PASSES (coincidentally, since the current handler always returns `Healthy`/`200`); `GetHealth_AContributorIsUnhealthy_ReturnsServiceUnavailable` and `GetHealth_ADegradedContributor_StillReturnsOk` FAIL — the current handler ignores the injected fake contributor entirely and never returns `503`.

- [ ] **Step 3: Update the `Health` handler**

In `src/Adapters.Rest/RestAdapterEndpointExtensions.cs`, find:

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Runtime.Agent;
using Runtime.Agent.Security;
```

Replace with:

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Runtime.Agent;
using Runtime.Agent.Health;
using Runtime.Agent.Security;
```

Then find:

```csharp
    private static IResult Health() => Results.Ok(new RestHealthResponse("Healthy"));
```

Replace with:

```csharp
    private static IResult Health(IEnumerable<IRuntimeHealthContributor> contributors)
    {
        var aggregate = RuntimeHealthAggregator.Aggregate(contributors);
        var response = new RestHealthResponse(aggregate.Status.ToString());

        return aggregate.Status == RuntimeHealthStatus.Unhealthy
            ? Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Ok(response);
    }
```

- [ ] **Step 4: Register the real contributor in `Program.cs`**

In `src/Host/Program.cs`, find:

```csharp
builder.Services.AddSingleton<ISecurityEvaluator, TrustedDefaultSecurityEvaluator>();
builder.Services.AddHostedService<RuntimeStartupHostedService>();
```

Replace with:

```csharp
builder.Services.AddSingleton<ISecurityEvaluator, TrustedDefaultSecurityEvaluator>();
builder.Services.AddSingleton<IRuntimeHealthContributor, AiProviderReachabilityHealthContributor>();
builder.Services.AddHostedService<RuntimeStartupHostedService>();
```

Then find the `using` block's `Runtime.Agent.Security` line:

```csharp
using Runtime.Agent.Security;
```

Replace with:

```csharp
using Runtime.Agent.Health;
using Runtime.Agent.Security;
```

- [ ] **Step 5: Build and run the tests**

Run: `dotnet build AefPlatform.sln`
Expected: Build succeeded, 0 errors.

Run: `dotnet test tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj --filter "FullyQualifiedName~HealthEndpointTests"`
Expected: all 3 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Adapters.Rest/RestAdapterEndpointExtensions.cs src/Host/Program.cs tests/Adapters.Rest.Tests/HealthEndpointTests.cs
git commit -m "feat(adapters.rest): wire the health endpoint to real contributor aggregation"
```

---

### Task 4: Structured per-request logging and metrics

**Files:**
- Create: `src/Adapters.Rest/RequestObservabilityMiddleware.cs`
- Create: `src/Adapters.Rest/RestRequestMetrics.cs`
- Modify: `src/Host/Program.cs`
- Test: `tests/Adapters.Rest.Tests/RequestObservabilityMiddlewareTests.cs` (create)
- Test: `tests/Adapters.Rest.Tests/RestRequestMetricsTests.cs` (create)

**Interfaces:**
- Produces: `Adapters.Rest.RequestObservabilityMiddlewareExtensions.UseRequestObservability(this IApplicationBuilder) : IApplicationBuilder`, `Adapters.Rest.RestRequestMetrics.RecordRequest(PathString, int, TimeSpan)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Adapters.Rest.Tests/RestRequestMetricsTests.cs`:

```csharp
using System.Diagnostics.Metrics;

namespace Adapters.Rest.Tests;

public class RestRequestMetricsTests
{
    [Fact]
    public void RecordRequest_EmitsARequestCountMeasurement()
    {
        long? observedCount = null;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "AefPlatform.Adapters.Rest" && instrument.Name == "aef.rest.requests")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) => observedCount = measurement);
        listener.Start();

        RestRequestMetrics.RecordRequest("/api/v1/health", 200, TimeSpan.FromMilliseconds(5));

        Assert.Equal(1, observedCount);
    }
}
```

Create `tests/Adapters.Rest.Tests/RequestObservabilityMiddlewareTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj --filter "FullyQualifiedName~RestRequestMetricsTests|FullyQualifiedName~RequestObservabilityMiddlewareTests"`
Expected: build FAILS — `RestRequestMetrics`/`RequestObservabilityMiddlewareExtensions` do not exist.

- [ ] **Step 3: Implement both files**

Create `src/Adapters.Rest/RestRequestMetrics.cs`:

```csharp
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;

namespace Adapters.Rest;

public static class RestRequestMetrics
{
    private static readonly Meter Meter = new("AefPlatform.Adapters.Rest");
    private static readonly Counter<long> RequestCount = Meter.CreateCounter<long>("aef.rest.requests");
    private static readonly Counter<long> ErrorCount = Meter.CreateCounter<long>("aef.rest.errors");
    private static readonly Histogram<double> RequestLatencyMs = Meter.CreateHistogram<double>("aef.rest.request_duration_ms");

    public static void RecordRequest(PathString path, int statusCode, TimeSpan elapsed)
    {
        var tags = new KeyValuePair<string, object?>("path", path.Value);
        RequestCount.Add(1, tags);
        RequestLatencyMs.Record(elapsed.TotalMilliseconds, tags);
        if (statusCode >= 500 || statusCode == StatusCodes.Status400BadRequest || statusCode == StatusCodes.Status403Forbidden)
        {
            ErrorCount.Add(1, tags);
        }
    }
}
```

Create `src/Adapters.Rest/RequestObservabilityMiddleware.cs`:

```csharp
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Adapters.Rest;

public static class RequestObservabilityMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestObservability(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<RequestObservabilityMiddlewareMarker>>();
            var stopwatch = Stopwatch.StartNew();

            logger.LogInformation("Request started: {Method} {Path}", context.Request.Method, context.Request.Path);

            try
            {
                await next(context);
            }
            finally
            {
                stopwatch.Stop();
                RestRequestMetrics.RecordRequest(context.Request.Path, context.Response.StatusCode, stopwatch.Elapsed);
                logger.LogInformation(
                    "Request completed: {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds}ms",
                    context.Request.Method, context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
            }
        });
}

// Marker type for the ILogger<T> category name only - never instantiated.
public sealed class RequestObservabilityMiddlewareMarker
{
    private RequestObservabilityMiddlewareMarker() { }
}
```

- [ ] **Step 4: Wire the middleware into `Program.cs`**

In `src/Host/Program.cs`, find:

```csharp
app.UseSwagger();
app.UseSwaggerUI();

app.MapGroup(restOptions.BasePath).MapRestAdapterEndpoints();
```

Replace with:

```csharp
app.UseRequestObservability();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGroup(restOptions.BasePath).MapRestAdapterEndpoints();
```

- [ ] **Step 5: Build and run the tests**

Run: `dotnet build AefPlatform.sln`
Expected: Build succeeded.

Run: `dotnet test tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj --filter "FullyQualifiedName~RestRequestMetricsTests|FullyQualifiedName~RequestObservabilityMiddlewareTests"`
Expected: both tests PASS.

- [ ] **Step 6: Re-run the full `Adapters.Rest.Tests` suite to confirm the new middleware doesn't break any existing endpoint test**

Run: `dotnet test tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj`
Expected: all tests (pre-existing plus this task's 2 new ones) PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Adapters.Rest/RequestObservabilityMiddleware.cs src/Adapters.Rest/RestRequestMetrics.cs src/Host/Program.cs tests/Adapters.Rest.Tests/RequestObservabilityMiddlewareTests.cs tests/Adapters.Rest.Tests/RestRequestMetricsTests.cs
git commit -m "feat(adapters.rest): add structured per-request logging and BCL request metrics"
```

---

### Task 5: Graceful shutdown configuration

**Files:**
- Modify: `src/Host/RestAdapterOptions.cs`
- Modify: `src/Host/Program.cs`
- Modify: `src/Host/appsettings.json`
- Test: `tests/Host.Tests/HostOptionsShutdownTests.cs` (create)

**Interfaces:**
- Produces: `RestAdapterOptions.ShutdownTimeoutSeconds` (int, default `30`) applied to `Microsoft.Extensions.Hosting.HostOptions.ShutdownTimeout`.

- [ ] **Step 1: Write the failing test**

Create `tests/Host.Tests/HostOptionsShutdownTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Host.Tests;

public class HostOptionsShutdownTests
{
    [Fact]
    public void ShutdownTimeout_ConfiguredFromRestAdapterOptions()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RestAdapter:ShutdownTimeoutSeconds"] = "45",
        });

        var restOptions = builder.Configuration.GetSection("RestAdapter").Get<RestAdapterOptions>() ?? new RestAdapterOptions();
        builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(restOptions.ShutdownTimeoutSeconds));

        using var host = builder.Build();
        var hostOptions = host.Services.GetRequiredService<IOptions<HostOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(45), hostOptions.ShutdownTimeout);
    }

    [Fact]
    public void ShutdownTimeout_DefaultsTo30Seconds()
    {
        var options = new RestAdapterOptions();

        Assert.Equal(30, options.ShutdownTimeoutSeconds);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails to compile**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj --filter "FullyQualifiedName~HostOptionsShutdownTests"`
Expected: build FAILS — `RestAdapterOptions.ShutdownTimeoutSeconds` does not exist.

- [ ] **Step 3: Add the option**

In `src/Host/RestAdapterOptions.cs`, find:

```csharp
public sealed class RestAdapterOptions
{
    [Range(1, 65535)]
    public int Port { get; set; } = 5080;

    public string BasePath { get; set; } = "/api/v1";

    public long MaxRequestBodyBytes { get; set; } = 1_048_576;
}
```

Replace with:

```csharp
public sealed class RestAdapterOptions
{
    [Range(1, 65535)]
    public int Port { get; set; } = 5080;

    public string BasePath { get; set; } = "/api/v1";

    public long MaxRequestBodyBytes { get; set; } = 1_048_576;

    public int ShutdownTimeoutSeconds { get; set; } = 30;
}
```

- [ ] **Step 4: Configure `HostOptions` in `Program.cs`**

In `src/Host/Program.cs`, find:

```csharp
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = restOptions.MaxRequestBodyBytes);
builder.WebHost.UseUrls($"http://localhost:{restOptions.Port}");
```

Replace with:

```csharp
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = restOptions.MaxRequestBodyBytes);
builder.WebHost.UseUrls($"http://localhost:{restOptions.Port}");
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(restOptions.ShutdownTimeoutSeconds));
```

- [ ] **Step 5: Add the setting to `appsettings.json`**

In `src/Host/appsettings.json`, find:

```json
  "RestAdapter": {
    "Port": 5080,
    "BasePath": "/api/v1",
    "MaxRequestBodyBytes": 1048576
  },
```

Replace with:

```json
  "RestAdapter": {
    "Port": 5080,
    "BasePath": "/api/v1",
    "MaxRequestBodyBytes": 1048576,
    "ShutdownTimeoutSeconds": 30
  },
```

- [ ] **Step 6: Build and run the tests**

Run: `dotnet build AefPlatform.sln`
Expected: Build succeeded.

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj --filter "FullyQualifiedName~HostOptionsShutdownTests"`
Expected: both tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Host/RestAdapterOptions.cs src/Host/Program.cs src/Host/appsettings.json tests/Host.Tests/HostOptionsShutdownTests.cs
git commit -m "feat(host): configure graceful-shutdown timeout via RestAdapterOptions"
```

---

### Task 6: Full regression run and documentation

**Files:**
- Modify: `README.md`
- Modify: `docs/api/rest-adapter-reference.md`

- [ ] **Step 1: Run the full solution test suite**

Run: `dotnet test AefPlatform.sln`
Expected: all tests pass — 441 pre-existing plus this Capability's new tests (3 `RuntimeHealthAggregatorTests` + 3 `AiProviderReachabilityHealthContributorTests` + 3 `HealthEndpointTests` [2 pre-existing replaced, 1 new — net +1 vs. before] + 1 `RestRequestMetricsTests` + 1 `RequestObservabilityMiddlewareTests` + 2 `HostOptionsShutdownTests` = 13 net new), 454+ total, 0 failures.

- [ ] **Step 2: Update the REST API reference**

In `docs/api/rest-adapter-reference.md`, find:

```markdown
## `GET /api/v1/health`

Returns `{ "status": "Healthy" }` unconditionally, `200 OK`. Real health-contributor logic is Capability 4's chartered scope — this is an honest placeholder, not yet a live signal.
```

Replace with:

```markdown
## `GET /api/v1/health`

Returns `{ "status": "Healthy" | "Degraded" | "Unhealthy" }`, aggregated from every registered `Runtime.Agent.Health.IRuntimeHealthContributor` (currently one: AI-provider network reachability). `200 OK` for `Healthy`/`Degraded`; `503 Service Unavailable` for `Unhealthy` — a distinct HTTP outcome from this Capability, not part of the `/api/v1/execution` error-mapping table.
```

- [ ] **Step 3: Update `README.md`'s Status section**

In `README.md`, find the line starting with:

```
**AEF v1.0 GA-3 Capability 3 (CLI Adapter) complete, 2026-07-08.**
```

Add a new paragraph immediately **before** it:

```markdown
**AEF v1.0 GA-3 Capability 4 (Cross-Cutting Hardening) complete, 2026-07-08.** Fourth Capability of the External Execution Channels program — see `docs/superpowers/specs/2026-07-08-ga3-capability4-cross-cutting-hardening-design.md` for the full design. Adds `Runtime.Agent.Health` (`IRuntimeHealthContributor`/`RuntimeHealthReport`/`RuntimeHealthStatus`/`RuntimeHealthAggregator`, additive to `Runtime.Agent`, mirroring the Capability 1 security-seam precedent) and one real contributor, `AiProviderReachabilityHealthContributor` — `GET /api/v1/health` now aggregates real signals instead of Capability 2's honest fixed placeholder, returning `503 Service Unavailable` when the configured AI provider is unreachable. `Adapters.Rest` gains structured per-request logging (method, path, status, duration — closing the Completion Gate's "logs are sufficient to reconstruct what a given request did without consulting `IEvidenceSink`") and BCL-only `System.Diagnostics.Metrics` request counters/latency histogram (`dotnet-counters`-observable, no vendor dependency). `HostOptions.ShutdownTimeout` is now configured (default `30s`) via `RestAdapterOptions.ShutdownTimeoutSeconds`. No CLI changes — Capability 3's log-free design stands. No `Runtime.Ai`, `Runtime.Agent`'s existing GA-2 contracts, `EngineeringExecutionPipeline`, or Capability 1-3 execution behavior changes. Full suite: 454+ passing.

**AEF v1.0 GA-3 Capability 3 (CLI Adapter) complete, 2026-07-08.**
```

- [ ] **Step 4: Commit and push**

```bash
git add README.md docs/api/rest-adapter-reference.md
git commit -m "docs: record GA-3 Capability 4 (Cross-Cutting Hardening) completion"
git push -u origin ga3-capability4-cross-cutting-hardening-design
```
