# GA-3 Capability 2: REST Adapter — Design

**Status:** Independent Engineering Review complete (Approved with minor findings). All four findings resolved in this revision: `Adapters.Rest`'s `FrameworkReference` to `Microsoft.AspNetCore.App` is now explicit (Decision 1); the "distinct HTTP outcome" completion-gate reading is now explicit (Decision 9); the `"Unknown session."` string-match risk is documented as accepted and test-covered rather than fixed by editing a frozen GA-2 file (Decision 9); the `ArchitectureBoundaryTests` citation is corrected to name the actual positive-control test. Ready for the Implementation Plan. Cites `docs/superpowers/specs/2026-07-08-ga3-external-execution-channels-engineering-program.md` (GA-3), `docs/superpowers/specs/2026-07-08-ga3-capability1-runtime-host-design.md` (Capability 1), and `ai-engineering-framework/epics/decisions/0025-public-execution-interface-boundary.md` (ADR-0025) rather than re-deriving them.

## Mission

GA-3 §4 Capability 2 is "hosted inside Capability 1's host once it exists" (now merged). This document designs the first live, product-facing channel into `Runtime.Agent`: a versioned HTTP surface over `IAgentExecutionInterface`, hosted inside the same `AefPlatform.Host` process Capability 1 built, invoking the `ISecurityEvaluator` seam before every execution.

## Frozen Inputs (not redesigned by this document)

- RC0, `Runtime.Ai`, `Runtime.Agent` (every contract: `ExecutionRequest`/`ExecutionResponse`, `IAgentExecutionInterface`, `ISessionStore`, `ICapabilityCatalog`, `ISecurityEvaluator`/`SecurityContext`/`SecurityDecision`).
- `EngineeringExecutionPipeline`.
- ADR-0025 — adapters depend on `Runtime.Agent` only; `Runtime.Agent` is the sole execution boundary.
- Capability 1's Runtime Host: `IHostedService`-based adapter attachment, `TrustedDefaultSecurityEvaluator`, the two concurrency fixes.

## Non-Goals

CLI, Chat, IDE, SDK, batch execution, streaming (SSE/WebSockets), authentication providers, identity providers, OAuth, JWT, rate limiting, caching, product-specific endpoints. No real health-contributor logic (Capability 4) — this Capability only adds the HTTP endpoint that will later expose it. No new configuration framework — extends the existing `IOptions<T>` convention only.

## Decision 1 — Project Structure and ADR-0025 Enforcement

New class library `src/Adapters.Rest` (namespace `Adapters.Rest`), referencing **`Runtime.Agent` only** — mirroring `Runtime.Agent.csproj`'s own reference to `Runtime` only. It contains the endpoint-mapping extension method, request/response translation, and validation — no execution logic. `AefPlatform.Host` (the composition root, already exempt from the "one dependency" rule per ADR-0025 Option 4 and Capability 1's precedent) adds a project reference to `Adapters.Rest` and calls its single public entry point to wire the HTTP pipeline into the same process Capability 1 already boots.

**Why a separate project, not code inside `Host`:** GA-3 §3 requires "an architecture fitness test (a dependency-direction check over project references...)" for "the two new adapter projects" (plural) — a fitness test needs a project reference to check. Folding REST endpoint code directly into `Host` would make the ADR-0025 boundary a convention again, not a compile-time guarantee, repeating the exact problem ADR-0025 exists to prevent.

**Fitness test:** `tests/Adapters.Rest.Tests/ArchitectureBoundaryTests.cs` asserts `Adapters.Rest.dll`'s referenced assemblies exclude `Runtime`, `Runtime.Ai`, `Kernel` — the same exclusion pattern `Runtime.Agent.Tests/ArchitectureBoundaryTests.cs` already established (`RuntimeAgentAssembly_DoesNotReferenceKernel`/`RuntimeAgentAssembly_DoesNotReferenceRuntimeAi`, reflecting over `GetReferencedAssemblies()`). The positive-control half ("does reference `Runtime.Agent`") mirrors `Runtime.Agent.Tests/AgentExecutionServiceTests.RuntimeAgentAssembly_NowReferencesRuntime` instead — the two assertions live in different existing files by precedent, not one.

**`Adapters.Rest.csproj`'s full reference set:** a `ProjectReference` to `Runtime.Agent.csproj` (the only *project* it depends on, which is what ADR-0025's rule and this fitness test are actually about) plus a `<FrameworkReference Include="Microsoft.AspNetCore.App" />` — required because `RestSecurityContextFactory.FromHttpContext` and the endpoint-mapping extension method (Decision 5) use `HttpContext`/`IEndpointRouteBuilder`, which a plain `Microsoft.NET.Sdk` class library cannot see without it. A shared-framework reference is not a project reference and is orthogonal to ADR-0025's dependency-direction rule (every other project in this solution that needs BCL types has an implicit, unstated framework reference the same way) — the fitness test checks project references only, exactly as `Runtime.Agent.Tests`'s own precedent does.

## Decision 2 — Hosting Model: `WebApplication` Replaces `HostApplicationBuilder`

`src/Host/Host.csproj`'s SDK changes from `Microsoft.NET.Sdk` to `Microsoft.NET.Sdk.Web` (the standard, zero-NuGet way to gain Kestrel/routing/minimal APIs — `Microsoft.AspNetCore.App` is a shared framework reference from the .NET SDK, not a NuGet package). `Program.cs`'s `Host.CreateApplicationBuilder(args)` becomes `WebApplication.CreateBuilder(args)`. This is additive and mechanical: `WebApplicationBuilder.Services` is the same `IServiceCollection` Capability 1's DI graph already populates — every existing `builder.Services.AddXxx(...)` line is unchanged. `WebApplication` itself implements `IHost`, so `RuntimeStartupHostedService` (an `IHostedService`) keeps running exactly as today, unmodified — Capability 1's "adapters are `IHostedService`" model is preserved, not replaced; REST's HTTP endpoints are mapped on the `WebApplication` after `Build()`, immediately before `RunAsync()`.

**Why not a separate REST process:** GA-3 §3 is explicit — "the REST Adapter is its first hosted adapter" — meaning REST lives inside the one long-running Runtime Host process, not a second executable. `WebApplication.CreateBuilder` is the only supported .NET 8 mechanism to add an HTTP server to a `Microsoft.Extensions.Hosting`-based process; the older `IHostBuilder.ConfigureWebHostDefaults` path does not apply here since Capability 1 already committed to the newer `HostApplicationBuilder` API.

**Test-visibility note:** `Program.cs` uses top-level statements, which the compiler emits as an `internal` `Program` class by default — invisible to `Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory<Program>` (Testing Plan, below) from another assembly. A single-line marker, `public partial class Program { }`, is added at the end of `Program.cs` — the standard, minimal, additive fix for this exact situation; it changes no existing behavior.

## Decision 3 — Minimal APIs, Not Controllers

Four (plus one, see Decision 5) small, stateless endpoints with no shared mutable state map naturally onto ASP.NET Core Minimal APIs (`app.MapPost`/`app.MapGet`) rather than MVC controllers — avoids the MVC ceremony (controller base classes, action filters, routing attributes) for a surface this small, and keeps every endpoint's translation-only responsibility visible in one place per Decision 1's "no business logic" constraint.

## Decision 4 — OpenAPI: `Swashbuckle.AspNetCore` (new NuGet dependency, justified)

Capability 1's "no new NuGet package" constraint was scoped to its own narrow, non-HTTP surface. OpenAPI generation is an explicit Goal of this Capability and cannot be produced without either a real generator package or a hand-maintained static document that silently drifts from the endpoints it describes — the worse of the two options. `Swashbuckle.AspNetCore` (stable, net8.0-supported, the long-standing standard for ASP.NET Core OpenAPI/Swagger generation) is added to `src/Host/Host.csproj` only. `builder.Services.AddEndpointsApiExplorer()` + `AddSwaggerGen()`, `app.UseSwagger()` + `app.UseSwaggerUI()` (development/diagnostic convenience, not a new execution surface).

## Decision 5 — Endpoints

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/v1/execution` | Execute an engineering request. |
| `GET` | `/api/v1/capabilities` | Capability discovery (`ICapabilityCatalog.Discover()` via `IAgentExecutionInterface.DiscoverCapabilities()`). |
| `POST` | `/api/v1/sessions` | Start a session (`IAgentExecutionInterface.StartSession`) — chartered by GA-3 §4 Capability 2's own Epics ("session-carrying endpoint semantics") even though the Capability 2 brief's "minimum endpoints" list omits it; included here because `ExecutionRequest.SessionId` is otherwise unreachable over HTTP with no way to obtain one. |
| `GET` | `/api/v1/health` | Placeholder health signal (see Decision 7). |
| `GET` | `/api/v1/version` | Static version string. |

### `POST /api/v1/execution`

Request body (new, additive DTO — **not** `Runtime.Agent.ExecutionRequest` itself, which carries a non-JSON-serializable `IExecutionProgressObserver?`; streaming is a Non-Goal, so this field is never exposed):

```csharp
namespace Adapters.Rest;

public sealed record RestExecutionRequest(string? SessionId, string UserRequest, string ProjectProfileId, IReadOnlyDictionary<string, object>? Payload);
```

`RequestId` is generated by the adapter (`Guid.NewGuid().ToString("N")`), not supplied by the caller — no existing caller convention for client-supplied request ids exists anywhere in `Runtime.Agent`, and inventing one is out of scope. The handler: (1) deserializes `RestExecutionRequest`, (2) validates `UserRequest`/`ProjectProfileId` are non-empty (see Decision 6), (3) builds a `SecurityContext` from the HTTP request (Decision 8), (4) calls `ISecurityEvaluator.Evaluate` — on denial, returns `403 Forbidden` with a `ProblemDetails` body, never reaching `Runtime.Agent`, (5) builds `Runtime.Agent.ExecutionRequest(Guid.NewGuid().ToString("N"), request.SessionId, request.UserRequest, request.ProjectProfileId, request.Payload)`, (6) calls `IAgentExecutionInterface.Execute`, (7) maps the returned `ExecutionResponse` to an HTTP response per Decision 9. The response body **is** `Runtime.Agent.ExecutionResponse` directly — every field is already JSON-serializable (records of strings, enums, nested records, `TimeSpan`, `DateTimeOffset`) — satisfying "do not duplicate execution contracts" for the outbound direction; only the inbound direction needs a thin translation type.

### `GET /api/v1/capabilities`

No request body. Calls `IAgentExecutionInterface.DiscoverCapabilities()`, returns `IReadOnlyList<CapabilityDescriptor>` directly as the JSON body — again no duplicated contract. `200 OK` always (an empty catalog is a valid, if unlikely, response — not an error).

### `POST /api/v1/sessions`

Request body: `RestStartSessionRequest(string? ProjectProfileId)` — a one-field DTO, since `IAgentExecutionInterface.StartSession(string?)` takes only that. Calls `StartSession`, returns the resulting `Runtime.Agent.ExecutionSession` directly as the JSON body. `201 Created` (a new session resource was created), no `Location` header (no `GET /api/v1/sessions/{id}` endpoint exists or is chartered — out of scope).

### `GET /api/v1/health`

See Decision 7 — a fixed, honest placeholder, not a duplicated or anticipatory Capability 4 mechanism.

### `GET /api/v1/version`

Returns `{ "version": "<assembly informational version>" }` read once at startup via `Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()`, falling back to `GetName().Version` if absent. No request body. `200 OK` always.

## Decision 6 — Request Validation

Two layers, both before `Runtime.Agent` is ever called:

1. **Transport-level** (ASP.NET Core's own minimal-API model binding): malformed JSON → `400 Bad Request` (ASP.NET Core's default `BadHttpRequestException` handling, no custom code needed); wrong `Content-Type` on a body-carrying request → `415 Unsupported Media Type` (also framework-default behavior for JSON-bound minimal API parameters).
2. **Domain-shape validation** (adapter-owned, since `Runtime.Agent`'s contracts carry no `[Required]`/`DataAnnotations` attributes of their own — they are plain DTOs, and Capability 1's principle of never modifying GA-2 contracts extends to not retrofitting validation attributes onto them): a small `static class RestExecutionRequestValidator` checks `UserRequest`/`ProjectProfileId` are non-null and non-whitespace on `RestExecutionRequest`, and `ProjectProfileId` is non-null and non-whitespace on `RestStartSessionRequest` (nullable per `StartSession(string? projectProfileId)`, so an entirely absent/null `ProjectProfileId` is valid — only an explicitly-supplied empty/whitespace string is rejected). A validation failure returns `400 Bad Request` with a `ProblemDetails` body naming the invalid field(s) — never reaching `ISecurityEvaluator` or `Runtime.Agent`.

## Decision 7 — Health Endpoint: Honest Placeholder, Not Anticipated Capability 4 Scope

`GET /api/v1/health` returns `200 OK` with `{ "status": "Healthy" }` unconditionally. This directly continues Capability 1's own resolved precedent (an empty health-contributor set is the correct, honest "Healthy" state, not a placeholder bug) rather than reopening Capability 1's Independent-Engineering-Review-resolved decision to defer all health-contributor logic to Capability 4. GA-3 §4 Capability 4 itself states the real signal is "used by Capability 1's host, exposed by Capability 2's REST Adapter" — meaning Capability 4 will later replace this handler's body with a real aggregate check (e.g. AI-provider reachability) without changing its route, request shape, or this Capability's completion status. Documented explicitly here so it is not mistaken for scope creep into Capability 4's chartered work.

## Decision 8 — Security Seam Invocation

```csharp
namespace Adapters.Rest;

internal static class RestSecurityContextFactory
{
    public static Runtime.Agent.Security.SecurityContext FromHttpContext(HttpContext httpContext) =>
        new(PrincipalId: httpContext.User.Identity?.Name, Claims: null);
}
```

Called by the `/api/v1/execution` handler immediately after validation, before constructing the `Runtime.Agent.ExecutionRequest`. With `TrustedDefaultSecurityEvaluator` (no production authentication is implemented — per this Capability's explicit brief — `httpContext.User` is always unauthenticated, so `PrincipalId` is always `null`), `Evaluate` always returns `IsAllowed: true`; the call exists to prove and exercise the seam end-to-end over real HTTP, not to gate access yet. On `IsAllowed: false` (exercised in tests via DI substitution, exactly as Capability 1's `GA3Capability1RuntimeHostTests` proved substitutability): `403 Forbidden`, `ProblemDetails` with `Detail` set to `SecurityDecision.DenialReason`, and `Runtime.Agent` is never called — verifiable via a spy/counting test double, mirroring `AgentExecutionServiceTests`' own "pipeline is never invoked" assertion style from GA-2.

## Decision 9 — Error Mapping

`Runtime.Agent`'s `ExecutionStatus` enum does not carry enough granularity to safely infer a unique HTTP status for every cause of `Failed` (e.g. "no dispatch target" and "validation command failed" are both `Failed` with no machine-readable discriminator beyond a free-text `Error.Reason`) — this is a real, documented limitation, not an oversight:

| Condition | HTTP Status | Body |
|---|---|---|
| `ExecutionStatus.Completed` | `200 OK` | `ExecutionResponse` |
| `ExecutionStatus.NeedsClarification` | `200 OK` | `ExecutionResponse` (caller inspects `Status`/`SuggestedNextStep`) |
| `ExecutionStatus.Failed`, `Error.Reason == "Unknown session."` | `404 Not Found` | `ExecutionResponse` — the one `Failed` cause with a stable, checkable discriminator (`AgentExecutionService.UnknownSessionResponse`'s literal reason string, already committed, tested GA-2 behavior) and a genuine "referenced resource does not exist" HTTP meaning. |
| `ExecutionStatus.Failed`, any other reason | `200 OK` | `ExecutionResponse` — a legitimate, honestly-reported business outcome (e.g. a failed validation run), not an HTTP-layer error; the HTTP call itself succeeded. |
| Validation failure (Decision 6) | `400 Bad Request` | `ProblemDetails` |
| Security denial (Decision 8) | `403 Forbidden` | `ProblemDetails` |
| Unhandled exception anywhere in the adapter | `500 Internal Server Error` | `ProblemDetails` with a generic `Detail` (`"An unexpected error occurred."`) — never the exception's message or stack trace, per "do not expose internal exceptions." Installed via `app.UseExceptionHandler(...)` configured once at startup, using `Microsoft.AspNetCore.Http.IProblemDetailsService` (`builder.Services.AddProblemDetails()`, a framework-provided service, no new package). |

This gives five genuinely distinct HTTP outcomes (200, 400, 403, 404, 500) driven by real, checkable conditions, rather than force-fitting `ExecutionStatus`'s three values onto three status codes using an inference the contract cannot actually support.

**Explicit reinterpretation of the completion gate:** GA-3 §9's "every `ExecutionStatus` value maps to a distinct, documented HTTP outcome" is read here as *status code plus response body jointly* constitutes the "HTTP outcome" — not "each of the three enum values gets its own unique status code." `Completed` and `NeedsClarification` share `200 OK` but are trivially distinguished by the caller reading `ExecutionResponse.Status` in the body, exactly as `SuggestedNextStep`/`Error` already differ between them. This reading is deliberate, not an oversight: `Runtime.Agent`'s own "escalate, never fabricate" philosophy treats a well-formed `NeedsClarification` or ordinary `Failed` result as a legitimate, successfully-processed outcome, not a transport-layer failure — forcing every enum value into a distinct status code would misrepresent a correctly-functioning HTTP call as an HTTP error. Flagged here explicitly for the Implementation Plan and Engineering Review to accept or override.

**Accepted risk — the `"Unknown session."` string match (not fixed by editing `Runtime.Agent`):** this literal is private to `AgentExecutionService.UnknownSessionResponse` — a frozen GA-2 file (see Frozen Inputs) — and is not promoted to a shared public constant, since doing so would edit a file this Capability's own constraints forbid touching for anything short of a genuine bug (Capability 1's precedent: only real concurrency defects justified editing a frozen file, not a robustness nicety). If that string is ever renamed inside `Runtime.Agent`, this Capability's own end-to-end test — "`POST /api/v1/execution` with an unknown `SessionId` → `404 Not Found`" (Testing Plan), which exercises the real `AgentExecutionService`, not a stub — fails immediately, since the 404 branch would stop matching. This converts "silent break" into "loud, already-covered test failure," which is the safety net, not a compiler signal.

## Decision 10 — Configuration

New `RestAdapterOptions` (same `IOptions<T>` + `ValidateDataAnnotations()` + `ValidateOnStart()` convention as `PlatformHostOptions`/`AiProviderOptions`):

```csharp
namespace AefPlatform.Host;

public sealed class RestAdapterOptions
{
    [Range(1, 65535)]
    public int Port { get; set; } = 5080;

    public string BasePath { get; set; } = "/api/v1";

    public long MaxRequestBodyBytes { get; set; } = 1_048_576; // 1 MiB
}
```

Bound from a new `"RestAdapter"` `appsettings.json` section, applied via `builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = options.Value.MaxRequestBodyBytes)` and `builder.WebHost.UseUrls($"http://localhost:{options.Value.Port}")`. `BasePath` is read once at endpoint-mapping time to prefix all four (five) routes — a `RouteGroupBuilder` (`app.MapGroup(basePath)`), not hardcoded `/api/v1` literals in each `MapGet`/`MapPost` call. No HTTPS-specific option is added beyond what `Microsoft.NET.Sdk.Web`'s own default Kestrel/ASP.NET Core configuration already provides (`appsettings.json`'s standard `Kestrel` section, unmodified) — introducing a bespoke HTTPS option would be a new configuration paradigm, contradicted by this Capability's own "do not introduce a new configuration framework" constraint.

## Decision 11 — Logging

No new abstraction, continuing Capability 1's Decision 3 precedent: `ILogger<T>` only. The exception handler (Decision 9) logs unhandled exceptions at `Error` level with the request path and method, never the caller-supplied payload (may carry a repository path or other request content not meant for logs by default). No per-request structured operational logging middleware is added — that is explicitly Capability 4's chartered work.

## Testing Plan

- `ArchitectureBoundaryTests` (`Adapters.Rest.Tests`): `Adapters.Rest.dll` references `Runtime.Agent` only.
- `RestExecutionRequestValidatorTests`: empty/whitespace `UserRequest`/`ProjectProfileId` rejected; a valid request passes.
- Endpoint tests (`Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory<Program>`, the standard ASP.NET Core integration-test harness — a new test-only NuGet package, `Microsoft.AspNetCore.Mvc.Testing`, alongside `Swashbuckle.AspNetCore`, both justified by this Capability's HTTP surface exactly as Decision 4 justifies Swashbuckle):
  - `POST /api/v1/execution` with a valid body against a real, in-memory-configured runtime → `200 OK`, `ExecutionResponse` body with `Status: Completed` for a request routing to a known capability.
  - `POST /api/v1/execution` with an unknown `SessionId` → `404 Not Found`.
  - `POST /api/v1/execution` with empty `UserRequest` → `400 Bad Request`.
  - `POST /api/v1/execution` with malformed JSON body → `400 Bad Request`.
  - `POST /api/v1/execution` with `Content-Type: text/plain` → `415 Unsupported Media Type`.
  - `POST /api/v1/execution` with `ISecurityEvaluator` substituted (test-only DI override, mirroring Capability 1's own substitutability test) for a denying evaluator → `403 Forbidden`, and `Runtime.Agent` is never invoked (a counting spy `IAgentExecutionInterface` test double proves zero calls).
  - `GET /api/v1/capabilities` → `200 OK`, body matches `EngineeringAgentCapabilityCatalog`'s 8 known descriptors (same count GA-2's own `GA2AgentExecutionHostTests` already asserts).
  - `POST /api/v1/sessions` → `201 Created`, body has a non-empty `SessionId`.
  - `GET /api/v1/health` → `200 OK`, `{"status":"Healthy"}`.
  - `GET /api/v1/version` → `200 OK`, non-empty `version` field.
  - `GET /openapi/swagger` (or Swashbuckle's default `/swagger/v1/swagger.json`) → `200 OK`, valid JSON parseable as an OpenAPI document (a smoke test, not full schema validation).
- Full existing suite (400 tests through Capability 1) stays green — re-run as the final gate.

## Documentation Updates (Definition of Done)

- Architecture doc: `Adapters.Rest` as the first hosted adapter, the `WebApplication` hosting change, the fitness test.
- API reference: all five endpoints, request/response shapes, status codes (Decision 9's table).
- Configuration guide: `RestAdapterOptions`, the new `appsettings.json` `RestAdapter` section.
- Deployment notes: Kestrel default binding, `MaxRequestBodyBytes`, no HTTPS/auth configured beyond framework defaults (explicit "not production-hardened yet" callout, consistent with the Non-Goals).

## Review

- ✓ `Runtime.Agent` remains the sole public execution boundary — `Adapters.Rest` never references `Runtime`/`Runtime.Ai`/`Kernel`, enforced by a fitness test.
- ✓ No existing GA-1/GA-2/Capability-1 file's contract changes; `Host`'s SDK/`Program.cs` changes are additive (new using, new `WebApplication` builder, new endpoint mapping) — no existing DI registration line is altered.
- ✓ Security seam invoked before every `Runtime.Agent` call, proven substitutable via a denying-evaluator test.
- ✓ Health deferred to Capability 4 in substance, not merely in name — the endpoint exists, its logic does not yet.
- ✓ No Non-Goal item (CLI, Chat, IDE, SDK, batch, streaming, auth providers, OAuth, JWT, rate limiting, caching, WebSockets, SSE, product-specific endpoints) appears anywhere in this design.
