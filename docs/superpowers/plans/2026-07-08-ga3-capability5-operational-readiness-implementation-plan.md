# GA-3 Capability 5: Operational Readiness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close every remaining GA-3 §9 release-readiness gate per `docs/superpowers/specs/2026-07-08-ga3-capability5-operational-readiness-design.md`: a REST contract-compatibility test, a real concurrent-HTTP-request test, live dogfooding through both channels against an external repository, measured performance figures, the remaining documentation guides, and a consolidated Release Readiness report.

**Architecture:** Pure additive tests (`Adapters.Rest.Tests`) plus temporary, never-committed validation tests (deleted before the final commit) plus new documentation. No existing endpoint, command, contract, or configuration default changes.

**Tech Stack:** .NET 8, xUnit, `System.Text.Json` (for `JsonValueKind` contract assertions — BCL, no new package).

## Global Constraints

- No existing GA-1/GA-2/Capability-1/-2/-3/-4 contract, endpoint, command, or configuration default changes.
- No new NuGet package dependency.
- Dogfooding tests (Task 3) are temporary — written, run, their output captured into the Release Readiness report, then deleted before the final commit. `git status` must show no new file remaining in `tests/` from Task 3 once Task 6 commits.
- The full existing test suite (453 tests through Capability 4) must stay green throughout — re-run as the final gate.

---

### Task 1: REST contract-compatibility test

**Files:**
- Test: `tests/Adapters.Rest.Tests/ContractCompatibilityTests.cs` (create)

**Interfaces:**
- Consumes: `Runtime.Agent.ExecutionResponse`, `Runtime.Agent.Capabilities.CapabilityDescriptor`, `Runtime.Agent.ExecutionSession` — exercised via real HTTP calls through the existing `WebApplicationFactory<Program>` fixture (same pattern every other `Adapters.Rest.Tests` file already uses).

- [ ] **Step 1: Write the test**

Create `tests/Adapters.Rest.Tests/ContractCompatibilityTests.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Adapters.Rest.Tests;

// GA-3 §5's Contract Compatibility row requires a real contract test, not
// just the /v1 URL segment. Verified against the real, live-serialized
// JSON (not assumptions) - see docs/superpowers/specs/2026-07-08-ga3-
// capability5-operational-readiness-design.md Decision 1. Property-name
// presence AND JsonValueKind per property, closing the type-change gap
// the design's own review flagged - not a full JSON-schema validator.
public class ContractCompatibilityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ContractCompatibilityTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ExecutionResponse_Contract_HasExpectedShapeAndTypes()
    {
        using var client = _factory.CreateClient();
        var request = new RestExecutionRequest(null, "start a new feature", "default-profile", null);

        var response = await client.PostAsJsonAsync("/api/v1/execution", request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        AssertProperty(root, "requestId", JsonValueKind.String);
        AssertProperty(root, "sessionId", JsonValueKind.Null);
        AssertProperty(root, "status", JsonValueKind.Number);
        AssertProperty(root, "error", JsonValueKind.Null);
        AssertProperty(root, "presentedResult", JsonValueKind.String);
        AssertProperty(root, "suggestedNextStep", JsonValueKind.String);
        AssertProperty(root, "evidence", JsonValueKind.Object);
        AssertProperty(root, "metadata", JsonValueKind.Object);

        var evidence = root.GetProperty("evidence");
        AssertProperty(evidence, "diagnostics", JsonValueKind.Array);

        var metadata = root.GetProperty("metadata");
        AssertProperty(metadata, "executedCapabilityId", JsonValueKind.String);
        AssertProperty(metadata, "duration", JsonValueKind.String);
        AssertProperty(metadata, "startedAt", JsonValueKind.String);
        AssertProperty(metadata, "completedAt", JsonValueKind.String);
    }

    [Fact]
    public async Task CapabilityDescriptor_Contract_HasExpectedShapeAndTypes()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/capabilities");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var first = document.RootElement[0];

        AssertProperty(first, "capabilityId", JsonValueKind.String);
        AssertProperty(first, "displayName", JsonValueKind.String);
        AssertProperty(first, "description", JsonValueKind.String);
        AssertProperty(first, "requiredPayloadKeys", JsonValueKind.Array);
        AssertProperty(first, "executionMode", JsonValueKind.String);
    }

    [Fact]
    public async Task ExecutionSession_Contract_HasExpectedShapeAndTypes()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/sessions", new RestStartSessionRequest(null));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        AssertProperty(root, "sessionId", JsonValueKind.String);
        AssertProperty(root, "createdAt", JsonValueKind.String);
        AssertProperty(root, "lastActivityAt", JsonValueKind.String);
        AssertProperty(root, "lastProjectProfileId", JsonValueKind.Null);
        AssertProperty(root, "lastPayload", JsonValueKind.Null);
        AssertProperty(root, "turnCount", JsonValueKind.Number);
    }

    private static void AssertProperty(JsonElement element, string propertyName, JsonValueKind expectedKind)
    {
        Assert.True(element.TryGetProperty(propertyName, out var property), $"Expected property '{propertyName}' was not found.");
        Assert.Equal(expectedKind, property.ValueKind);
    }
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj --filter "FullyQualifiedName~ContractCompatibilityTests"`
Expected: all 3 tests PASS against the current, unmodified REST Adapter — this task adds a regression test for existing behavior, it does not change any behavior. If any assertion fails, the JSON shape recorded above was observed by hand-running the CLI/REST responses beforehand; a failure here means the actual shape differs from what was captured — investigate before assuming the test is wrong.

- [ ] **Step 3: Commit**

```bash
git add tests/Adapters.Rest.Tests/ContractCompatibilityTests.cs
git commit -m "test(adapters.rest): add the REST contract-compatibility test"
```

---

### Task 2: Concurrent-request test

**Files:**
- Test: `tests/Adapters.Rest.Tests/ConcurrentExecutionTests.cs` (create)

**Interfaces:**
- Consumes: `WebApplicationFactory<Program>`, `Runtime.Agent.Sessions.ISessionStore` (resolved directly from the factory's DI container to observe `TurnCount` — Capability 2 has no `GET /api/v1/sessions/{id}` endpoint).

- [ ] **Step 1: Write the test**

Create `tests/Adapters.Rest.Tests/ConcurrentExecutionTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Runtime.Agent;
using Runtime.Agent.Sessions;

namespace Adapters.Rest.Tests;

// Closes Capability 1's concurrency audit "verified again under a real
// concurrent-load test" gate for real (GA-3 §8) - Capability 2's own
// Testing Plan was sequential-only, so this had never actually run
// through the real REST pipeline before. 20 is a small multiple of a
// typical dev machine's minimum thread-pool worker count, chosen to
// force genuine interleaving of the InMemoryEvidenceSink/
// InMemorySessionStore singletons under WebApplicationFactory's real
// concurrent dispatch - not a production load-test figure.
public class ConcurrentExecutionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const int ConcurrentRequestCount = 20;

    private readonly WebApplicationFactory<Program> _factory;

    public ConcurrentExecutionTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ConcurrentRequests_WithoutSharedSession_AllSucceedWithUniqueRequestIds()
    {
        using var client = _factory.CreateClient();

        var tasks = Enumerable.Range(0, ConcurrentRequestCount)
            .Select(_ => client.PostAsJsonAsync("/api/v1/execution", new RestExecutionRequest(null, "start a new feature", "default-profile", null)))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<ExecutionResponse>()));
        Assert.All(bodies, b => Assert.Equal(ExecutionStatus.Completed, b!.Status));
        Assert.Equal(ConcurrentRequestCount, bodies.Select(b => b!.RequestId).Distinct().Count());
    }

    [Fact]
    public async Task ConcurrentRequests_SharingOneSession_NoLostTurnCountUpdates()
    {
        using var client = _factory.CreateClient();
        var sessionResponse = await client.PostAsJsonAsync("/api/v1/sessions", new RestStartSessionRequest(null));
        var session = await sessionResponse.Content.ReadFromJsonAsync<ExecutionSession>();

        var tasks = Enumerable.Range(0, ConcurrentRequestCount)
            .Select(_ => client.PostAsJsonAsync("/api/v1/execution", new RestExecutionRequest(session!.SessionId, "start a new feature", "default-profile", null)))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        // No GET /api/v1/sessions/{id} endpoint exists (Capability 2) - the
        // final TurnCount is observed directly from the factory's own DI
        // container, a test-only shortcut, not a claim this is reachable
        // over HTTP today.
        var sessionStore = _factory.Services.GetRequiredService<ISessionStore>();
        var finalSession = sessionStore.Get(session!.SessionId);

        Assert.NotNull(finalSession);
        Assert.Equal(ConcurrentRequestCount, finalSession!.TurnCount);
    }
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj --filter "FullyQualifiedName~ConcurrentExecutionTests"`
Expected: both tests PASS — Capability 1's fixes (the lock in `InMemoryEvidenceSink`, the atomic `AddOrUpdate` in `InMemorySessionStore.Touch`) should already make these pass without further code changes. If `ConcurrentRequests_SharingOneSession_NoLostTurnCountUpdates` fails with `TurnCount` less than 20, that would indicate the earlier fix does not hold under real HTTP concurrency — stop and investigate; do not adjust the assertion to make it pass.

- [ ] **Step 3: Commit**

```bash
git add tests/Adapters.Rest.Tests/ConcurrentExecutionTests.cs
git commit -m "test(adapters.rest): add a real concurrent-HTTP-request test, closing Capability 1's concurrency-audit gate"
```

---

### Task 3: Live dogfooding through both channels (temporary tests, not committed)

**Files:**
- Temporary (not committed): `tests/Cli.Tests/DogfoodingFerretTests.cs`
- Temporary (not committed): `tests/Adapters.Rest.Tests/DogfoodingFerretTests.cs`

**Interfaces:**
- Consumes: the real `C:\POC\Ferret` repository, `CliExecutableFixture.Run` (Capability 3), `WebApplicationFactory<Program>` (Capability 2).

- [ ] **Step 1: Write the temporary CLI dogfooding test**

Create `tests/Cli.Tests/DogfoodingFerretTests.cs` (temporary — do not commit):

```csharp
namespace Cli.Tests;

public class DogfoodingFerretTests
{
    [Fact]
    public void Execute_RepositoryIntelligenceAgainstFerret_ReturnsHonestNonFabricatedResult()
    {
        var (exitCode, stdout, _) = CliExecutableFixture.Run(
            "execute",
            "--user-request", "Analyze this repository's architecture and code quality signals.",
            "--project-profile-id", "default-profile",
            "--payload-json", "{\"RepositoryName\":\"Ferret\",\"RepositoryRoot\":\"C:\\\\POC\\\\Ferret\"}");

        Assert.Equal(0, exitCode);
        Assert.Contains("Completed", stdout);
    }
}
```

- [ ] **Step 2: Run it and capture the real output verbatim**

Run: `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~DogfoodingFerretTests" -v n`
Record: the exit code and the full `stdout` (add a temporary `Console.WriteLine(stdout)` inside the test if xUnit's own output doesn't surface it, or run the equivalent `dotnet src/Cli/bin/Debug/net8.0/AefPlatform.Cli.dll execute ...` command directly in a terminal to see the raw text) into a scratch note for Task 6's report — verbatim, not paraphrased, matching RC0's own "honest, evidence-grounded findings" standard.

- [ ] **Step 3: Write the temporary REST dogfooding test**

Create `tests/Adapters.Rest.Tests/DogfoodingFerretTests.cs` (temporary — do not commit):

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Runtime.Agent;

namespace Adapters.Rest.Tests;

public class DogfoodingFerretTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DogfoodingFerretTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task PostExecution_RepositoryIntelligenceAgainstFerret_ReturnsHonestNonFabricatedResult()
    {
        using var client = _factory.CreateClient();
        var payload = new Dictionary<string, object> { ["RepositoryName"] = "Ferret", ["RepositoryRoot"] = "C:\\POC\\Ferret" };
        var request = new RestExecutionRequest(null, "Analyze this repository's architecture and code quality signals.", "default-profile", payload);

        var response = await client.PostAsJsonAsync("/api/v1/execution", request);
        var body = await response.Content.ReadFromJsonAsync<ExecutionResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ExecutionStatus.Completed, body!.Status);
    }
}
```

- [ ] **Step 4: Run it and capture the real output verbatim**

Run: `dotnet test tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj --filter "FullyQualifiedName~DogfoodingFerretTests" -v n`
Record: the response body's `PresentedResult`, `Evidence.Diagnostics`, and `Status` verbatim for Task 6's report.

- [ ] **Step 5: Delete both temporary test files — do not commit them**

```bash
rm tests/Cli.Tests/DogfoodingFerretTests.cs tests/Adapters.Rest.Tests/DogfoodingFerretTests.cs
git status
```

Expected: `git status` shows no new/modified file from this task — both were temporary, matching RC0 P4's own "never committed" precedent, since they depend on the external, environment-specific `C:\POC\Ferret` path that will not exist in CI.

---

### Task 4: Live performance measurement (recorded, not a committed test)

**Files:** none committed — this task produces recorded figures for Task 6's report only.

- [ ] **Step 1: Measure Host cold-boot time**

Run: `time dotnet src/Host/bin/Debug/net8.0/AefPlatform.Host.dll &` then poll `curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5080/api/v1/health` in a loop until it returns `200`/`503` (either is "booted"); record the wall-clock delta. Stop the process afterward (`kill` the backgrounded PID).

- [ ] **Step 2: Measure CLI per-invocation startup time**

Run: `time dotnet src/Cli/bin/Debug/net8.0/AefPlatform.Cli.dll capabilities` — record the real wall-clock time.

- [ ] **Step 3: Measure Host idle working set**

With the Host running (Step 1, before stopping it), inspect the process's working set (e.g. `Get-Process` on Windows, or `ps -o rss` on Linux/macOS) a few seconds after the health check first succeeds, before issuing any other request. Record the value in MB.

- [ ] **Step 4: Measure REST adapter-layer latency**

With the Host running, issue the same deterministic `"start a new feature"` request via `curl -w "%{time_total}\n"` against `POST /api/v1/execution` a handful of times; record the range.

- [ ] **Step 5: Record all four figures** for Task 6's Release Readiness report, alongside RC0's own baseline figures for comparison (RC0: ~1.0–1.1s cold boot, ~41.5MB idle working set, 533–711ms for a real Repository Intelligence request).

---

### Task 5: Remaining documentation guides

**Files:**
- Create: `docs/guides/developer-guide.md`, `docs/guides/operations-guide.md`, `docs/guides/deployment-guide.md`

- [ ] **Step 1: Write the Developer Guide**

Create `docs/guides/developer-guide.md`:

```markdown
# Developer Guide

## Calling `IAgentExecutionInterface` in-process

`Runtime.Agent.IAgentExecutionInterface` (`AgentExecutionService`) is the sole, product-facing execution boundary (ADR-0025). A caller in the same process:

```csharp
var agent = serviceProvider.GetRequiredService<IAgentExecutionInterface>();
var response = agent.Execute(new ExecutionRequest(
    RequestId: Guid.NewGuid().ToString("N"),
    SessionId: null,
    UserRequest: "start a new feature",
    ProjectProfileId: "default-profile",
    Payload: null));
```

`response.Status` is one of `ExecutionStatus.Completed`, `NeedsClarification`, or `Failed`. `DiscoverCapabilities()` lists every dispatchable capability; `StartSession(projectProfileId)` begins a multi-turn session.

## Running `AefPlatform.Host` locally

```bash
dotnet run --project src/Host
```

Listens on `http://localhost:5080` by default (`RestAdapter:Port`, `appsettings.json`). `GET /api/v1/health` confirms it booted.

## Running `AefPlatform.Cli` locally

```bash
dotnet run --project src/Cli -- capabilities
dotnet run --project src/Cli -- execute --user-request "start a new feature" --project-profile-id default-profile
```

## Repository layout

- `src/Kernel`, `src/Runtime`, `src/Runtime.Ai`, `src/Runtime.Agent` — the frozen execution core (RC0 through GA-2).
- `src/Composition` — dependency-light classes shared by `Host` and `Cli` (Capability 3).
- `src/Adapters.Rest`, `src/Host` — the REST channel (Capability 2) and its composition root (Capability 1).
- `src/Adapters.Cli`, `src/Cli` — the CLI channel (Capability 3) and its own composition root.
```

- [ ] **Step 2: Write the Operations Guide**

Create `docs/guides/operations-guide.md`:

```markdown
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
```

- [ ] **Step 3: Write the Deployment Guide**

Create `docs/guides/deployment-guide.md`:

```markdown
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

## Trusted-boundary assumption

No HTTPS termination and no real authentication/authorization are configured — `ISecurityEvaluator`'s trusted-default implementation authorizes every request (Capability 1). This program assumes a trusted network boundary (e.g. behind an internal load balancer or VPN) terminates TLS and enforces access control externally, or that a future GA replaces the trusted-default `ISecurityEvaluator` with a real implementation — the seam already exists and is proven substitutable (Capability 1's `GA3Capability1RuntimeHostTests`, Capability 2's `ExecutionEndpointTests.PostExecution_SecurityDenies_ReturnsForbiddenAndNeverCallsRuntimeAgent`) without touching `Runtime.Agent` or any adapter.
```

- [ ] **Step 4: Commit**

```bash
git add docs/guides
git commit -m "docs: add the Developer, Operations, and Deployment guides"
```

---

### Task 6: Full regression, Release Readiness report, and final README update

**Files:**
- Create: `docs/GA-3/AEF-v1.0-GA-3-Release-Readiness.md`
- Modify: `README.md`

- [ ] **Step 1: Run the full solution test suite**

Run: `dotnet test AefPlatform.sln`
Expected: all tests pass — 453 pre-existing plus this Capability's 5 new, permanent tests (3 `ContractCompatibilityTests` + 2 `ConcurrentExecutionTests`), 458 total, 0 failures. (The temporary dogfooding tests from Task 3 are already deleted and do not appear in this count.)

- [ ] **Step 2: Write the Release Readiness report**

Create `docs/GA-3/AEF-v1.0-GA-3-Release-Readiness.md`, populated with:
- A Capability Matrix (one row per Capability 1–5, status, design/plan doc links).
- The GA-3 §9 checklist, each line checked off with a link to its evidence (test file, design doc section, or a subsection of this report).
- The verbatim dogfooding output captured in Task 3, for both CLI and REST, against `C:\POC\Ferret`.
- The performance figures measured in Task 4, compared against RC0's own baseline.
- A Test Summary: final pass count (458/458), 0 warnings.
- Known Limitations: no real authentication (trusted-boundary assumption, Deployment Guide), no metrics vendor bundled, `RuntimeRegistryHealthContributor` not built (Capability 4's own documented, deliberate omission), REST's error-mapping table treats most `Failed` outcomes as `200 OK` (Capability 2's own documented, deliberate design choice) — carried forward here as the final program's own honest limitations list, not silently dropped.

- [ ] **Step 3: Update `README.md`'s Status section**

In `README.md`, find the line starting with:

```
**AEF v1.0 GA-3 Capability 4 (Cross-Cutting Hardening) complete, 2026-07-08.**
```

Add a new paragraph immediately **before** it:

```markdown
**AEF v1.0 GA-3 (External Execution Channels) complete, 2026-07-08.** All five Capabilities of the GA-3 program are complete and merged — see `docs/GA-3/AEF-v1.0-GA-3-Release-Readiness.md` for the full release-readiness report against every GA-3 §9 gate: Architecture Complete, Channels Complete (including a new REST contract-compatibility test and a real concurrent-HTTP-request test closing Capability 1's concurrency audit for real), Documentation Complete (`docs/guides/developer-guide.md`, `operations-guide.md`, `deployment-guide.md`, alongside the existing CLI/REST references), Performance Validated (measured against RC0's baseline), Regression Complete (458/458 passing), Engineering Execution Validated (live dogfooding through both CLI and REST against the real, external `Ferret` repository, mirroring RC0 P4's own precedent), and Security Seam Validated. AEF now exposes `Runtime.Agent` through two independent, production-shaped channels (REST, CLI) with a shared security seam, real health signal, structured logging, and basic metrics — built entirely on the frozen RC0/GA-1/GA-2 execution core, with zero redesign of `Runtime`, `Runtime.Ai`, `Runtime.Agent`'s existing contracts, or `EngineeringExecutionPipeline` anywhere across all five Capabilities.

**AEF v1.0 GA-3 Capability 4 (Cross-Cutting Hardening) complete, 2026-07-08.**
```

- [ ] **Step 4: Commit and push**

```bash
git add docs/GA-3 README.md
git commit -m "docs: add the GA-3 Release Readiness report and record program completion"
git push -u origin ga3-capability5-operational-readiness-design
```
