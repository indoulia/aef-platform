# Operations Guide

## Health

`GET /api/v1/health` aggregates every registered `IRuntimeHealthContributor` (currently: AI-provider network reachability). `200 OK` for `Healthy`/`Degraded`; `503 Service Unavailable` for `Unhealthy`. A `503` should prompt an operator to check the configured AI provider's network reachability first (`appsettings.json`'s `Ai:Provider`/`Ai:OpenAi:BaseUrl` or `Ai:AzureOpenAi:Endpoint`).

## Graceful shutdown

`HostOptions.ShutdownTimeout` (`RestAdapter:ShutdownTimeoutSeconds`, default `30`) governs how long the process waits for in-flight requests to complete before forcing a stop on `SIGTERM`/`Ctrl+C`.

## Logging

Every REST request logs a "Request started"/"Request completed" pair (method, path, status, duration) via `ILogger` at `Information` level — sufficient to reconstruct what a given request did without consulting governance evidence (`IEvidenceSink`).

## Metrics

`System.Diagnostics.Metrics` meter `AefPlatform.Adapters.Rest`: `aef.rest.requests`, `aef.rest.errors` (counters), `aef.rest.request_duration_ms` (histogram), tagged by request path. Observe with:

```bash
dotnet-counters monitor --process-id <pid> AefPlatform.Adapters.Rest
```

No metrics vendor is bundled — attach `dotnet-counters`, OpenTelemetry, or an equivalent as a deployment decision.

## Startup

`AefPlatform.Host` and `AefPlatform.Cli` both resolve their own `appsettings.json` relative to their own executable directory (`AppContext.BaseDirectory`), not the caller's working directory — both can be launched from anywhere. On startup, `RuntimeStartupHostedService` validates the full governed artifact registry; a failure aborts startup by default (`AefPlatform:FailFastOnRegistrationError: true`).

## Known operational limitations

- No real health-contributor beyond AI-provider reachability — a registry-load-failure contributor was considered and deliberately not built (Capability 4), since the default `FailFastOnRegistrationError: true` already aborts startup before any health check could observe such a failure.
- No metrics vendor, no distributed tracing — attach one as a deployment decision.
- No process-restart-on-crash mechanism — a deployment decision (e.g. a container orchestrator's own restart policy, `systemd`, or a Windows Service wrapper).
