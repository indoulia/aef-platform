# GA-3 Capability 4: Cross-Cutting Hardening — Design

**Status:** Independent Engineering Review complete (Approved with minor findings, all resolved): clarified the reachability contributor's timeout-vs-silent-hang handling, made the metrics vendor-neutrality claim precise (defers, doesn't eliminate, the listener decision), and added a forward-looking note on `Runtime.Agent` extension discipline. Ready for the Implementation Plan. Cites `docs/superpowers/specs/2026-07-08-ga3-external-execution-channels-engineering-program.md` (GA-3), Capabilities 1–3's design docs, and ADR-0025 rather than re-deriving them.

## Mission

GA-3 §4 Capability 4: "applied across both adapters once both exist, rather than duplicated per-adapter during Capabilities 2/3." Completion Gate: "A running host reports health honestly (fails readiness if a dependency, e.g. the configured AI provider, is unreachable); a shutdown signal drains in-flight requests instead of dropping them; logs are sufficient to reconstruct what a given request did without consulting `IEvidenceSink`." This document replaces Capability 2's honest health placeholder with real logic, adds structured per-request logging and basic request metrics to the REST Adapter, and configures graceful shutdown — the four items GA-3 §4/§5 charter to this Capability and no earlier one.

## Frozen Inputs (not redesigned by this document)

RC0, `Runtime.Ai`, `Runtime.Agent`'s existing GA-2 contracts, `EngineeringExecutionPipeline`, ADR-0025, Capability 1 (Runtime Host), Capability 2 (REST Adapter — its endpoint routes, request/response shapes, and error-mapping table for non-health endpoints), Capability 3 (CLI Adapter). This document's changes to Capability 2's `Health` handler and `Host`'s `Program.cs` are the explicit exception GA-3 itself charters — Capability 1's own design deferred real health logic here by name, and Capability 2's own design stated the placeholder would be "replaced... without changing its route, request shape, or this Capability's completion status."

## Non-Goals

CLI structured logging (Capability 3's own design explicitly scoped CLI as log-free — a short-lived, one-shot process's entire "log" is its own printed output and exit code; this Capability does not revisit that call). Any specific APM/metrics vendor integration (GA-3 §5: "vendor selection is a deployment decision"). Real identity-provider/authentication changes. Rate limiting, caching, streaming. A generic, pluggable health-check framework beyond what `Runtime.Agent.Health` already defines conceptually in this document — one real contributor is built, not an extensible catalog of many.

## Decision 1 — Health Contributor Contracts (additive to `Runtime.Agent`)

New files under `Runtime.Agent.Health`, mirroring `Runtime.Agent.Security`'s established additive-file precedent (Capability 1 Decision 2):

```csharp
namespace Runtime.Agent.Health;

public enum RuntimeHealthStatus { Healthy, Degraded, Unhealthy }

public sealed record RuntimeHealthReport(string Name, RuntimeHealthStatus Status, string? Reason);

public interface IRuntimeHealthContributor
{
    RuntimeHealthReport Check();
}

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

`RuntimeHealthStatus`'s declaration order (`Healthy` < `Degraded` < `Unhealthy`) is load-bearing — `Enumerable.Max` over the enum's default ordinal comparison is how "worst status wins" is implemented; this is documented here so a future reordering of the enum's members is recognized as a behavior change, not a cosmetic one. An empty contributor set aggregates to `Healthy` — the same "no contributors registered = trivially healthy" precedent Capability 1 already established, now genuinely populated by this Capability.

**Why this lives in `Runtime.Agent`, not `Host` or `Composition`:** REST's health handler (a `Runtime.Agent`-only project per ADR-0025) must be able to call `RuntimeHealthAggregator.Aggregate` and receive `RuntimeHealthReport`/`RuntimeHealthStatus` values — the same reasoning Capability 1 Decision 2 already established for `ISecurityEvaluator`. **Noted for future capabilities:** this is now the second additive namespace grafted onto `Runtime.Agent` (`Runtime.Agent.Security`, then `Runtime.Agent.Health`) under the identical justification — every future extension of `Runtime.Agent` should be held to the same test (does an ADR-0025-constrained adapter genuinely need this type to be reachable from `Runtime.Agent` itself), not added by reflex because the precedent now exists.

## Decision 2 — The One Real Contributor: AI Provider Reachability

`src/Host/AiProviderReachabilityHealthContributor.cs` (stays in `Host`, not `Composition` — `Composition`'s charter is "classes `Cli` needs too," and `Cli` has no health endpoint to serve; this class is REST/Host-specific):

```csharp
namespace AefPlatform.Host;

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
        var baseUrl = _options.Value.Provider == "AzureOpenAI"
            ? _options.Value.AzureOpenAi?.Endpoint
            : _options.Value.OpenAi?.BaseUrl;

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

Deliberately checks network *reachability* only — any HTTP response (including a `404`/`401` a bare `HEAD` to a chat-completions base URL will typically produce) counts as `Healthy`, since the question this contributor answers is "can we reach the configured host at all," not "are our credentials valid" or "does this exact path exist." A caught exception (DNS failure, connection refused, timeout) is the only `Unhealthy` path — including the case where a provider's edge/CDN doesn't cleanly reject `HEAD` with a `405` (which would itself count as `Healthy`, correctly) but instead silently hangs or drops the connection for an unsupported verb: the 3-second timeout converts that into a caught `TaskCanceledException`/`HttpRequestException`, still correctly reported as `Unhealthy` rather than hanging the health check indefinitely. This reuses the same `HttpClient` singleton `Program.cs` already registers for `IAiProvider` — no new HTTP client, no new configuration surface. `Check()` is synchronous (matching the interface Decision 1 defines) via `HttpClient.Send`, blocking the calling thread for up to 3 seconds on a real timeout — acceptable for a low-frequency health endpoint, not acceptable if this contributor pattern were ever reused on a hot path; noted here as a bounded, deliberate tradeoff.

**Why not a `RuntimeRegistryHealthContributor` too (considered and dropped):** the registry-load report (`RuntimeArchitectureLoader.Load`'s result) is already checked at startup by `RuntimeStartupHostedService`, which aborts the process (`PlatformHostOptions.FailFastOnRegistrationError`, default `true`) before the health endpoint could ever observe a failed report — under the default configuration this contributor would be permanently, trivially `Healthy`, adding surface area without a real signal. Wiring it up meaningfully would require `RuntimeStartupHostedService` to publish its report to a shared, mutable, DI-visible location — a structural change to a Capability 1 file for a contributor whose only payoff is the non-default `FailFastOnRegistrationError: false` configuration. Declined for this Capability; revisit if that configuration ever sees real use.

## Decision 3 — Wiring the REST Health Endpoint to Real Logic

`RestAdapterEndpointExtensions.Health` (Capability 2, `src/Adapters.Rest`) changes from a fixed `Results.Ok(new RestHealthResponse("Healthy"))` to:

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

`RestHealthResponse`'s shape is unchanged (`{ "status": "..." }`) — only the value populating `Status` changes, from the literal `"Healthy"` to the real aggregate's `ToString()` (`"Healthy"`, `"Degraded"`, or `"Unhealthy"`), so no existing consumer of the JSON shape breaks. `Degraded` still returns `200 OK` (still serving, worth surfacing in the body but not worth removing from a load balancer's rotation); only `Unhealthy` returns `503` — a new, distinct, and correctly-conventional HTTP outcome for this one endpoint, not a change to Capability 2's execution-endpoint error-mapping table (Decision 9 of that design), which this document does not touch.

`Program.cs` registers the one real contributor: `builder.Services.AddSingleton<IRuntimeHealthContributor, AiProviderReachabilityHealthContributor>();` — an `IEnumerable<IRuntimeHealthContributor>` resolves correctly from a single DI registration (the standard .NET DI behavior for multiply-registerable interfaces), and the same substitutability precedent Capability 1 established for `ISecurityEvaluator` (a test can register a second, conflicting/successful implementation to prove behavior) applies here too.

## Decision 4 — Structured Per-Request Logging (REST only)

New `src/Adapters.Rest/RequestObservabilityMiddleware.cs` — lives in `Adapters.Rest` (not `Host`), since it is pure ASP.NET Core middleware over `HttpContext`/`RequestDelegate` with no `Runtime`/`Runtime.Ai`/`Kernel` dependency, keeping REST-specific cross-cutting concerns alongside the rest of the REST Adapter's own code, consistent with Capability 2's existing file layout:

```csharp
namespace Adapters.Rest;

public static class RequestObservabilityMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestObservability(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<RequestObservabilityMiddleware>>();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

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

public sealed class RequestObservabilityMiddleware; // marker type for the ILogger<T> category name only - no instance is constructed
```

This satisfies the Completion Gate's "logs are sufficient to reconstruct what a given request did without consulting `IEvidenceSink`" literally: method, path, final status code, and duration, for every REST request — the operational log stream Capability 1 Decision 4 explicitly deferred out of Capability 1's scope. `Program.cs` calls `app.UseRequestObservability();` immediately after the exception-handler middleware and before endpoint mapping, so every request (including ones that hit the 400/403/404/500 paths) is logged. No caller-supplied payload content is logged — only transport-level metadata (method, path, status, duration) — matching Capability 2 Decision 9's "never expose internal exceptions"/no-sensitive-content-in-diagnostics discipline extended to logs.

## Decision 5 — Basic Request Metrics via `System.Diagnostics.Metrics` (no vendor, no new package)

`System.Diagnostics.Metrics.Meter`/`Counter<T>`/`Histogram<T>` are BCL types (part of the runtime since .NET 6) — no NuGet package, no vendor decision, satisfying GA-3 §5's explicit "vendor selection is a deployment decision, not a GA-3 architectural requirement." `src/Adapters.Rest/RestRequestMetrics.cs`:

```csharp
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

Observable externally via `dotnet-counters monitor AefPlatform.Cli` (or `AefPlatform.Host`) / any `System.Diagnostics.Metrics`-compatible listener (including OpenTelemetry, should a later GA choose it) with zero code change to this Capability — the Meter API is listener-agnostic by design, which is precisely why it satisfies "basic request metrics" without prescribing a vendor. **Precisely stated:** this defers the vendor decision, it does not eliminate the need for one — with no listener attached, these measurements are recorded but never observed anywhere; a deployment that wants operational value from them must still attach `dotnet-counters`, OpenTelemetry, or an equivalent, exactly as GA-3 §5 anticipates ("vendor selection is a deployment decision").

## Decision 6 — Graceful Shutdown

`RestAdapterOptions` gains one field:

```csharp
public int ShutdownTimeoutSeconds { get; set; } = 30;
```

`Program.cs` sets `builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(restOptions.ShutdownTimeoutSeconds));` — `HostOptions.ShutdownTimeout` is the Generic Host's own, already-existing mechanism (default `5` seconds) governing how long `IHost.StopAsync` waits for in-flight work (including Kestrel's own in-flight-request draining) before forcing a stop; this Capability does not invent a new shutdown mechanism, it configures the existing one to a value (`30s`) more appropriate for engineering requests that can run longer than simple CRUD calls. **Known testing limitation, stated honestly rather than glossed over:** `WebApplicationFactory`'s in-memory `TestServer` does not model real socket-level connection draining the way a live Kestrel listener does, so an integration test proving "a real in-flight HTTP request survives a real process shutdown signal" cannot be written reliably against `TestServer` — the Testing Plan below verifies the *configuration value* takes effect (`HostOptions.ShutdownTimeout` resolves to the configured value from a real built host) as the achievable, honest substitute; full drain behavior is asserted by design (this is exactly what `HostOptions.ShutdownTimeout` is documented by Microsoft to do) rather than re-proven end-to-end.

## Testing Plan

- `RuntimeHealthAggregatorTests` (new `Runtime.Agent.Tests` file... **frozen-input exception, stated explicitly**: `Runtime.Agent.Tests` already exists from GA-2/Capability 1 and gets one new test *file*, not a modification to any existing test — additive, same principle as Capability 1's own additive files): empty contributor list → `Healthy`; one `Healthy` + one `Unhealthy` → aggregate `Unhealthy` with both names in `Reason`; all `Healthy` → `Healthy`, `Reason: null`.
- `AiProviderReachabilityHealthContributorTests` (`Host.Tests`): a reachable URL (e.g. an `HttpClient` pointed at a `WebApplicationFactory`-hosted stub, or a well-known always-up test endpoint used elsewhere in this repo's own test conventions if one exists) → `Healthy`; an unreachable URL (e.g. `http://localhost:1` or a non-routable address) → `Unhealthy` within the 3-second timeout.
- `HealthEndpointTests` (`Adapters.Rest.Tests`, extending Capability 2's existing file): with the trusted-default contributor set (real `AiProviderReachabilityHealthContributor` against the placeholder `appsettings.json` OpenAI URL, which is genuinely reachable over the internet) → `200 OK`, `Status: "Healthy"`; with a DI-substituted always-`Unhealthy` test-double contributor (mirroring the `ISecurityEvaluator` substitutability precedent) → `503 Service Unavailable`, `Status: "Unhealthy"`.
- `RequestObservabilityMiddlewareTests` (`Adapters.Rest.Tests`): a request through a `WebApplicationFactory` with a test `ILoggerProvider` capturing log records → asserts a "Request started" and a "Request completed" record exist for a real call, with the correct status code and a non-negative duration captured.
- `RestRequestMetricsTests`: a `MeterListener` subscribed to `"AefPlatform.Adapters.Rest"` captures at least one measurement on `aef.rest.requests` after `RecordRequest` is called directly (unit-level, no HTTP round-trip needed for this specific assertion).
- `HostOptionsShutdownTests` (`Host.Tests`): build a real host with a non-default `ShutdownTimeoutSeconds` configured, resolve `IOptions<HostOptions>`, assert `.Value.ShutdownTimeout` equals the configured value.
- Full existing suite (441 tests through Capability 3) stays green — re-run as the final gate.

## Documentation Updates (Definition of Done)

- REST API reference (`docs/api/rest-adapter-reference.md`): update the `GET /api/v1/health` section — real statuses, the `503` case, what "Healthy"/"Degraded"/"Unhealthy" mean.
- Architecture doc: `Runtime.Agent.Health`'s contracts, the one real contributor, the logging/metrics middleware, the shutdown-timeout configuration.
- Operations note: how to observe `dotnet-counters` output for `AefPlatform.Host`; what a `503` from `/health` should prompt an operator to check (AI provider connectivity).

## Review

- ✓ `Runtime.Agent` remains the sole public execution boundary — the health seam is additive, mirroring the security seam's own precedent exactly.
- ✓ Capability 2's execution/capabilities/sessions/version endpoints and their error-mapping table are untouched — only `Health` changes, and only in the way GA-3 itself charters for this Capability.
- ✓ No new NuGet dependency — `System.Diagnostics.Metrics` is BCL; `HttpClient`/`HostOptions` are already-referenced framework types.
- ✓ No CLI logging/observability scope added, per Capability 3's own already-settled design.
- ✓ No APM/metrics vendor selected — the Meter API is deliberately vendor-neutral.
- ✓ The graceful-shutdown testing limitation is disclosed, not silently assumed away.
