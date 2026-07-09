# Deployment Guide

## Process model

One `dotnet` process per adapter: `AefPlatform.Host` (long-running, serves REST) and `AefPlatform.Cli` (short-lived, one process per command). Neither shares state with the other beyond `Runtime.Agent`'s own session model, which is process-local and in-memory only — sessions do not survive a `Host` restart.

## Configuration surface

`appsettings.json` (both `Host` and `Cli` carry their own copy — `AefPlatform`/`Ai` sections are shared in shape; `RestAdapter` is `Host`-only):

| Section | Key | Default | Purpose |
|---|---|---|---|
| `AefPlatform` | `PlatformName` | `"AEF Platform Runtime"` / `"AEF Platform CLI"` | Display name, logged at startup. |
| `AefPlatform` | `FailFastOnRegistrationError` | `true` | Abort startup on a registry-integrity failure. |
| `Ai` | `Provider` | `"OpenAI"` | `"OpenAI"` or `"AzureOpenAI"`. |
| `Ai` | `TimeoutSeconds` | `60` | Per-call AI provider timeout. |
| `RestAdapter` | `Port` | `5080` | Kestrel listen port (`Host` only). |
| `RestAdapter` | `BasePath` | `/api/v1` | Route prefix for every REST endpoint. |
| `RestAdapter` | `MaxRequestBodyBytes` | `1048576` | Kestrel request body size limit. |
| `RestAdapter` | `ShutdownTimeoutSeconds` | `30` | Graceful-shutdown drain window. |

All settings can be overridden via environment variables using the standard .NET double-underscore convention (e.g. `RestAdapter__Port=8080`) or command-line arguments (`Host` only — `Cli`'s own positional arguments are its command/flags, not configuration overrides).

## Trusted-boundary assumption

No HTTPS termination and no real authentication/authorization are configured — `ISecurityEvaluator`'s trusted-default implementation authorizes every request (Capability 1). This program assumes a trusted network boundary (e.g. behind an internal load balancer or VPN) terminates TLS and enforces access control externally, or that a future GA replaces the trusted-default `ISecurityEvaluator` with a real implementation — the seam already exists and is proven substitutable (Capability 1's `GA3Capability1RuntimeHostTests`, Capability 2's `ExecutionEndpointTests.PostExecution_SecurityDenies_ReturnsForbiddenAndNeverCallsRuntimeAgent`) without touching `Runtime.Agent` or any adapter.

## Measured performance (against RC0's baseline)

| Metric | RC0 baseline | GA-3 (this program) |
|---|---|---|
| Cold boot (Host) | ~1.0–1.1s | ~1.16s |
| CLI per-invocation startup | not applicable (no CLI existed) | ~0.30s |
| Idle working set (Host) | ~41.5MB | ~77.5MB (REST/health/logging/metrics infrastructure added) |
| Repository Intelligence request, real repo | 533–711ms | 602–608ms (CLI and REST, against `Ferret`) |
| Deterministic Feature Lifecycle request (REST, no AI) | not measured at RC0 | ~3–23ms steady-state (first request ~96ms, JIT warmup) |

No regression beyond what the added surface area (REST hosting, health, structured logging, metrics) accounts for.
