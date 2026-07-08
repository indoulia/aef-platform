# GA-2: AI Agent Integration Layer — Design

**Status:** Implemented. See `docs/superpowers/plans/2026-07-08-ga2-ai-agent-integration-layer-implementation-plan.md` for the implementation plan and `README.md` for the completion summary.

## Mission

RC0 and GA-1 are complete and merged (`main`). GA-1 gave `aef-platform` a provider-neutral `Runtime.Ai` (OpenAI + Azure OpenAI providers, `AiExecutionService`, `EngineeringPromptCatalog`) and used it to give `AGT-EXE-0013`/`AGT-EXE-0014` real implementations, fully wired into `EngineeringExecutionPipeline`. That architecture is frozen for GA-2.

Today, `EngineeringExecutionPipeline.Execute(EngineeringRequest) : ExecutionResult` is invoked only from tests — `Host`'s `Program.cs` boots DI, validates the capability registry, and then idles forever (`await host.RunAsync()`) with no live entry point of any kind.

GA-2's job: build the first stable, product-facing execution interface over the existing pipeline — request/response contracts, governed capability discovery, session abstraction, and a routing layer that maps external requests onto the pipeline's own existing dispatch — **without** wiring any live transport (CLI/REST/IDE/Chat). This stage is a channel-agnostic library plus tests. The first live channel is GA-3's job.

## Explicit Non-Goals (per the GA-2 brief)

No redesign of `Runtime.Ai`. No replacement of `EngineeringExecutionPipeline`. No plugin framework. No multi-agent orchestration or autonomous planning. No persistent/long-term memory or knowledge storage. No MCP. No live CLI/REST/IDE/Chat endpoint (deferred to GA-3). No new NuGet packages (BCL + `Microsoft.Extensions.Hosting/Options` only, matching the repo's existing convention).

## Architecture

New class library **`src/Runtime.Agent`** (namespace `Runtime.Agent`), sitting beside `Runtime.Ai`. It references **only `Runtime`** — not `Runtime.Ai`, not `Kernel` directly.

This is a compile-time governance guarantee, not just a convention: `Runtime.Agent` physically cannot call an `IAiProvider`, `AiExecutionService`, or any `IBehavior` directly. The only way into governed execution is `EngineeringExecutionPipeline.Execute()`, which already enforces routing (`SKL-EXE-0021`) and dispatch. "No bypass of the engineering workflow" is structural.

This boundary is now a standing architectural invariant for the whole program, recorded in `ai-engineering-framework/epics/decisions/0025-public-execution-interface-boundary.md` (**ADR-0025**): `Runtime.Agent` is the sole public execution boundary, `EngineeringExecutionPipeline` stays internal, and every future channel (GA-3's CLI/REST/IDE/Chat) must depend on `Runtime.Agent` only, never on `Runtime`/`Kernel` directly. See ADR-0025 for the full rationale and alternatives considered — it is not re-derived here.

`Host`'s only change: a few lines in `Program.cs` registering `AgentExecutionService`/`InMemorySessionStore`/`EngineeringAgentCapabilityCatalog` in DI. No configuration section is needed — there is nothing to configure (no ports, no endpoints).

## One Deliberate, Minimal Touch to `Runtime`

`ExecutionResult` collapses "needs clarification" and "genuinely unroutable/failed" into the same `Success: false` shape today. Verified in `EngineeringExecutionPipeline`: both the `RoutingOutcome.NeedsClarification` branch and the `Dispatch` default case ("No executable dispatch target...") leave `ExecutedWorkflowId`/`ExecutedAgentId` null with no other distinguishing field — a caller cannot tell them apart, and guessing from the shape is provably wrong (both paths look identical).

Per direction: this must be an **explicit outcome classification**, not a reinterpretation of `Success`. Add:

```csharp
namespace Runtime.Execution;

public enum ExecutionResultKind
{
    Unspecified = 0,
    Completed = 1,
    NeedsClarification = 2,
    Failed = 3,
}
```

`Completed` and `Failed` are reserved vocabulary, not populated by any call site in GA-2 — every existing return path keeps defaulting to `Unspecified` and is derived from `Success` exactly as before. Only the `NeedsClarification` branch is touched. Reserving the full set now means a future pipeline change that wants to populate `Completed`/`Failed` explicitly (e.g. to distinguish more failure shapes) doesn't require another contract change to `ExecutionResult` — just population of values that already exist.

...and one new optional trailing field on `ExecutionResult`:

```csharp
public sealed record ExecutionResult(
    bool Success,
    string? ExecutedWorkflowId,
    string? ExecutedAgentId,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string>? ValidationResults,
    TimeSpan Duration,
    string? PresentedResult,
    string? SuggestedNextStep,
    string? RepositoryAnalyzed = null,
    IReadOnlyList<string>? FilesChanged = null,
    bool? TestsExecuted = null,
    ExecutionResultKind Kind = ExecutionResultKind.Unspecified);
```

Exactly **one** existing call site changes — the `NeedsClarification` branch in `EngineeringExecutionPipeline.Execute` — to pass `Kind: ExecutionResultKind.NeedsClarification`. Every other existing constructor call (roughly a dozen, across every `From*` method) is untouched: trailing optional parameters, so `Success`'s meaning and every other return site is unaffected. This is the same additive-optional-parameter pattern GA-1 already used for `RepositoryAnalyzed`/`FilesChanged`/`TestsExecuted`.

`Runtime.Agent` derives its own public `ExecutionStatus` this way, without duplicating the pipeline's routing decision:

```
Kind == NeedsClarification                     -> ExecutionStatus.NeedsClarification
Kind == Completed || (Kind == Unspecified && Success)   -> ExecutionStatus.Completed
Kind == Failed    || (Kind == Unspecified && !Success)  -> ExecutionStatus.Failed
```

`ExecutionResultKind` (internal, `Runtime.Execution`) and `ExecutionStatus` (public, `Runtime.Agent`) are intentionally two different types — the pipeline's internal classification is not the same thing as the product-facing contract, and keeping them separate means either can evolve without forcing a change in the other.

## Public Contracts (`Runtime.Agent`, all `sealed record` unless noted)

- `ExecutionRequest(string RequestId, string? SessionId, string UserRequest, string ProjectProfileId, IReadOnlyDictionary<string,object>? Payload, IExecutionProgressObserver? ProgressObserver = null)`
- `ExecutionResponse(string RequestId, string? SessionId, ExecutionStatus Status, ExecutionError? Error, string? PresentedResult, string? SuggestedNextStep, ExecutionEvidence Evidence, ExecutionMetadata Metadata)`
- `enum ExecutionStatus { Completed, Failed, NeedsClarification }`
- `ExecutionError(string Reason, IReadOnlyList<string> Diagnostics)` — populated whenever `Status != Completed`
- `ExecutionEvidence(IReadOnlyList<string> Diagnostics, IReadOnlyList<string>? ValidationResults, string? RepositoryAnalyzed, IReadOnlyList<string>? FilesChanged, bool? TestsExecuted)` — a direct, lossless remap of `ExecutionResult`'s own evidentiary fields.
- `ExecutionMetadata(string? ExecutedCapabilityId, string? ExecutedAgentId, TimeSpan Duration, DateTimeOffset StartedAt, DateTimeOffset CompletedAt)`
- `ExecutionSession(string SessionId, DateTimeOffset CreatedAt, DateTimeOffset LastActivityAt, string? LastProjectProfileId, IReadOnlyDictionary<string,object>? LastPayload, int TurnCount)`

**Known non-goal:** `ExecutionEvidence` does not reach into `AiExecutionService`'s internal `IEvidenceSink` to surface individual AI-call evidence records. `EvidenceRecord` carries no request/session correlation id today, so attributing a given AI call to a given `ExecutionRequest` would require guessing (e.g. a timestamp window), which is unsafe under concurrent requests. `ExecutionEvidence` is built entirely from `ExecutionResult`'s own fields, which is lossless for everything the pipeline already reports.

## Capability Discovery

```csharp
public sealed record CapabilityDescriptor(
    string CapabilityId,
    string DisplayName,
    string Description,
    IReadOnlyList<string> RequiredPayloadKeys,
    string ExecutionMode);

public interface ICapabilityCatalog
{
    IReadOnlyList<CapabilityDescriptor> Discover();
}
```

`EngineeringAgentCapabilityCatalog : ICapabilityCatalog` is a hand-authored table — one entry per capability ID `EngineeringExecutionPipeline.Dispatch` already switches on (`WFL-DEL-0001`, `WFL-DEL-0004`, `WFL-DEL-0005`, `WFL-DEL-0006`, `WFL-DEL-0007`, `WFL-DEL-0008`, `SKL-EXE-0018`, `REPO-VALIDATION`). `ExecutionMode` is always `"Synchronous"` today — the field exists as the extension point the brief asks for ("supported execution modes"), not because multiple modes exist yet.

This mirrors the existing `EngineeringPromptCatalog`/`EngineeringArchitectureManifest` convention: manually maintained, not reflected out of the pipeline's private switch (there is no richer metadata anywhere to reflect — `EngineeringArchitectureManifest.KnownArtifacts` only carries `Id`+`Tier`). A test enumerates the same 8 literal IDs and asserts each has exactly one descriptor, keeping the two lists honestly in sync the same way `EngineeringArchitectureManifest` is kept in sync with the pipeline's switch today (manually, with a code comment marking the coupling).

## Session Model

```csharp
public interface ISessionStore
{
    ExecutionSession Create(string? projectProfileId);
    ExecutionSession? Get(string sessionId);
    ExecutionSession Touch(string sessionId, string? projectProfileId, IReadOnlyDictionary<string,object>? payload);
}
```

`InMemorySessionStore` — a plain `ConcurrentDictionary<string, ExecutionSession>`, process-lifetime only, never persisted to disk or any store. This satisfies "no persistent memory / no long-term knowledge storage" literally, while still delivering the "conversation/session support" the brief asks for.

Session state is pure caller convenience: a follow-up turn may omit `ProjectProfileId`/`Payload` and inherit the prior turn's values. It **never** influences routing or dispatch — those still happen exclusively inside `EngineeringExecutionPipeline`, per request, deterministically. This is how "conversation state must remain isolated from execution state" and "execution must remain deterministic" are both satisfied simultaneously: the session is a memory of *what the caller last said*, not a memory the pipeline consults.

An `ExecutionRequest` carrying an unknown `SessionId` returns an `ExecutionError` — the service does not silently fall back to a fresh session, since that could mask a caller-side bug (wrong id, expired process, typo) as a routing problem instead. `SessionId == null` is the normal one-off case: no session is created, resolved, or touched, and `ExecutionResponse.SessionId` is echoed back as `null`. Session tracking is opt-in per request, not implicit.

## The Router

```csharp
public interface IAgentExecutionInterface
{
    ExecutionResponse Execute(ExecutionRequest request);
    IReadOnlyList<CapabilityDescriptor> DiscoverCapabilities();
    ExecutionSession StartSession(string? projectProfileId);
}
```

`AgentExecutionService : IAgentExecutionInterface` — constructed with `EngineeringExecutionPipeline`, `ICapabilityCatalog`, `ISessionStore`. `Execute` is synchronous, matching the pipeline's own synchronous signature (no speculative `Task`-wrapping over a call that is not actually asynchronous anywhere underneath it).

Flow: if `SessionId` is non-null, resolve it (error if unknown) and merge its stored defaults into the request (explicit fields on the request always win); if `SessionId` is null, skip session handling entirely → build `EngineeringRequest(UserRequest, ProjectProfileId, Payload)` → call `pipeline.Execute(...)` (the single seam into governed execution) → if a session was resolved, touch it with this turn's values → map `ExecutionResult` → `ExecutionResponse`.

## Streaming Extension Point

```csharp
public interface IExecutionProgressObserver
{
    void OnProgress(ExecutionProgressEvent progressEvent);
}

public sealed record ExecutionProgressEvent(string RequestId, string Stage, string Message, DateTimeOffset Timestamp);
```

`EngineeringExecutionPipeline.Execute` is one blocking call with no internal progress hooks — there is nothing granular to report honestly. `AgentExecutionService` fires exactly two real events when a caller supplies an observer: `"Routing"` immediately before calling the pipeline, and a terminal event (`"Completed"`/`"Failed"`/`"NeedsClarification"`) immediately after. This is a real, minimal, wired extension point — not a dangling unused interface — without fabricating incremental steps the underlying pipeline cannot actually produce yet.

## Testing Plan

- `ExecutionResultKindTests` (`Runtime.Tests`): the `NeedsClarification` branch sets `Kind`; every other existing return path still defaults to `Unspecified` and is unaffected.
- `EngineeringAgentCapabilityCatalogTests`: all 8 known dispatchable IDs have exactly one descriptor with a non-empty description and non-empty `RequiredPayloadKeys` (where applicable); an unknown ID returns nothing.
- `InMemorySessionStoreTests`: `Create` produces empty state; `Get` on an unknown id returns null; `Touch` updates `LastActivityAt`/`TurnCount`/merges payload.
- `AgentExecutionServiceTests` (integration-style, reusing the repo's existing pattern of booting a real `RuntimeHandle` with stub behaviors, as `Runtime.Tests`'s own pipeline tests already do):
  - Completed path maps `ExecutedWorkflowId`/`AgentId`, `Diagnostics`, `ValidationResults`, `PresentedResult` correctly.
  - `NeedsClarification` vs. `Failed` are now distinguishable (previously provably ambiguous).
  - Session round-trip: first call creates and returns a `SessionId`; a second call reusing it and omitting `ProjectProfileId`/`Payload` inherits the prior turn's values.
  - Unknown `SessionId` supplied → `ExecutionError`, and the pipeline is never invoked (verifiable via a spy/counting stub).
  - `IExecutionProgressObserver` receives exactly the `Routing` and terminal events for one `Execute` call.
- Full existing suite (currently 324+ tests through RC0/GA-1) must stay green throughout — re-run as the final gate.

## Documentation Updates (Definition of Done)

- Architecture doc: new `Runtime.Agent` layer and its "references only `Runtime`" invariant.
- Component diagram: `Runtime.Agent` sitting beside `Runtime.Ai`, both consumed by `Host`.
- Public interfaces doc: the contract shapes above, marked as the stable surface for future channels.
- Execution flow doc: request → session resolution → `EngineeringExecutionPipeline.Execute` → response mapping.
- Developer docs: how to call `IAgentExecutionInterface` from a test or a future channel.
- No new governed manifest entries are required — `ExecutionRequest`/`ExecutionResponse`/etc. are implementation-only DTOs with no governed `PCT-`/`SCH-` counterpart, the same precedent `ExecutionResult` itself already set in GA-1 ("no governed schema exists for this generic shape... this is a genuine, implementation-only DTO").
