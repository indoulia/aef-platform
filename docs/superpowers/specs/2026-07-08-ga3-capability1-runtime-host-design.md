# GA-3 Capability 1: AEF Runtime Host — Design

**Status:** Independent Engineering Review complete (Approved with minor findings). Both findings resolved in this revision: the Health Seam (originally Decision 3) was removed as scope not chartered to this Capability by the GA-3 program document, and a DI-substitutability test was added for the security seam per GA-3 §9's Security Seam Validated gate. Ready for the Implementation Plan. Cites `docs/superpowers/specs/2026-07-08-ga3-external-execution-channels-engineering-program.md` (the GA-3 architecture/engineering program) and `ai-engineering-framework/epics/decisions/0025-public-execution-interface-boundary.md` (ADR-0025) rather than re-deriving them.

## Mission

GA-3's own Section 4 sequences Capability 1 first: "Nothing hosted can be built before the host exists, and the concurrency audit must land before REST accepts overlapping requests for real." Today, `Host`'s `Program.cs` boots the same DI graph GA-1/GA-2 already built, then idles forever (`await host.RunAsync()`) with zero hosted adapters. Capability 1's job is to turn that idle placeholder into a generic, adapter-hosting composition root, add the security abstraction seam GA-3 names as this Capability's own responsibility, and clear the one concurrency risk GA-3 calls out as its "one genuinely new technical risk" before any adapter introduces real concurrent callers.

## Frozen Inputs (not redesigned by this document)

- RC0 Runtime Foundation
- `Runtime.Ai` and `Runtime.Agent` (GA-1/GA-2) — every existing contract (`ExecutionRequest`/`ExecutionResponse`, `IAgentExecutionInterface`, `ISessionStore`/`InMemorySessionStore`, `ICapabilityCatalog`, `IExecutionProgressObserver`) is unchanged.
- `EngineeringExecutionPipeline` — unchanged, remains internal per ADR-0025.
- ADR-0025 — the dependency direction it establishes (`Runtime.Agent` → `Runtime` only; every channel → `Runtime.Agent` only) is extended to new adapter projects in a later Capability, not amended here.
- GA-3's own architecture/engineering program document — Capability sequencing, non-goals, and cross-cutting capability dispositions are taken as given.

## Non-Goals (this Capability only)

- No REST or CLI adapter code (Capabilities 2 and 3).
- No health/diagnostics abstraction of any kind, no structured operational/request logging policy — both are Capability 4's own chartered epics per the GA-3 program document §4 and are not pulled forward here, seam or otherwise.
- No new adapter-hosting abstraction beyond `IHostedService` (see Decision 1).
- No authentication/authorization policy, identity provider integration, or per-caller access control — only the seam and a trusted-default implementation.
- No general concurrency-hardening effort — only the two concrete races this audit identifies.
- No new NuGet package dependency (BCL + `Microsoft.Extensions.Hosting`/`Options`, matching the repo's existing convention).

## Decision 1 — Adapter Hosting Model

Adapters attach to the Runtime Host as plain `IHostedService` registrations — no new marker interface. This is the same lifecycle `RuntimeStartupHostedService` already uses today, and it is a BCL abstraction the Generic Host natively starts/stops in composition order, with no reflection or custom discovery needed.

**Rationale:** Today exactly one adapter (REST, Capability 2) will actually run inside this process — the CLI Adapter (Capability 3) is a separate, short-lived executable per GA-3 §3 and never attaches to this host. Inventing a richer `IHostedAdapter` abstraction (name/description metadata, adapter enumeration for diagnostics) for a single real implementer is speculative. `IHostedService` already gives the host everything it needs: ordered startup, graceful shutdown via `StopAsync`, and DI-based composition.

**Explicitly deferred:** If a future Capability introduces multiple hosted adapters that need common metadata, diagnostics, or host-managed lifecycle behavior beyond what `IHostedService` provides, an `IHostedAdapter` abstraction may be reconsidered at that time. Out of scope here.

## Decision 2 — Security Seam

New, additive files under `Runtime.Agent.Security` (no existing `Runtime.Agent` file is modified):

```csharp
namespace Runtime.Agent.Security;

public sealed record SecurityContext(string? PrincipalId, IReadOnlyDictionary<string, string>? Claims);

public sealed record SecurityDecision(bool IsAllowed, string? DenialReason);

public interface ISecurityEvaluator
{
    SecurityDecision Evaluate(SecurityContext context);
}
```

`TrustedDefaultSecurityEvaluator : ISecurityEvaluator` lives in `Host` (`AefPlatform.Host`) and unconditionally returns `new SecurityDecision(IsAllowed: true, DenialReason: null)`. It is registered as the DI default for `ISecurityEvaluator`.

**Contract:** each future adapter is responsible for constructing a `SecurityContext` from its own transport (HTTP headers for REST, local process identity for CLI), calling `ISecurityEvaluator.Evaluate` after transport-level parsing but *before* constructing or submitting an `ExecutionRequest`, and rejecting the request at the adapter boundary on `IsAllowed: false`. This keeps policy evaluation off the `Runtime.Agent` execution path entirely — `Runtime.Agent` never sees a request that failed the seam — while keeping adapters limited to translation-plus-seam-invocation, never policy logic of their own, per ADR-0025 Decision 3.

**Why this satisfies both constraints:** ADR-0025 requires every adapter to depend on `Runtime.Agent` only (enforced by a fitness test in a later Capability), so the seam's public contract must be reachable from `Runtime.Agent`'s own assembly. The mission's "Runtime.Agent is frozen" instruction is read as "the approved GA-2 contracts and architectural responsibilities are not re-derived or changed" — not as "the assembly can never gain an orthogonal, additive capability." No existing GA-2 file changes. The Runtime Host owns the security *service* (registers an implementation), not security *policy* — the trusted-default implementation is a development/runtime convenience; real authentication/authorization policy is a future capability's concern and can be swapped in via the same `ISecurityEvaluator` seam without touching `Runtime.Agent` or any adapter contract.

**Substitutability:** the seam is proven genuinely swappable, not just theoretically an interface, by registering a second implementation in a test — see Testing Plan. This directly satisfies GA-3 §9's Release Readiness gate: "the extension point is demonstrated ... not merely asserted in documentation."

**Health/diagnostics is explicitly out of scope for this Capability.** The GA-3 program document's own Capability 1 section (§4) charters only the adapter-hosting model, this security seam, and the concurrency audit — health/diagnostics reporting is Capability 4's chartered epic (§4, §5's Diagnostics row: "host-level health/diagnostics from Capability 4"). No health abstraction, seam, or extension point is introduced here; Capability 4's own design is where `IRuntimeHealthContributor`-shaped work (or equivalent) belongs, scoped and reviewed against its own chartering text rather than anticipated early.

## Decision 3 — Logging

No new abstraction. `Microsoft.Extensions.Logging`/`ILogger<T>` is already used by `RuntimeStartupHostedService`; this Capability standardizes on that same pattern for any new Host-owned code (e.g. logging when the trusted-default security evaluator is registered, at the `Debug` level, for local diagnosability). Structured per-request operational logging is explicitly Capability 4's job (distinct from `IEvidenceSink`'s governance-evidence recording, which is unrelated and unaffected).

## Decision 4 — Configuration

No new configuration surface. There is nothing to configure yet — no adapter exists, so no listen address, timeout, or adapter-enablement flag has a reason to exist in this Capability. Future adapter configuration extends the existing `IOptions<T>`-bound convention (`PlatformHostOptions`, `AiProviderOptions`) rather than introducing a new paradigm — documented here as a placeholder decision for Capability 2/3 to follow, not implemented now.

## Decision 5 — Concurrency Audit

GA-3 §1 names this Capability's concurrency audit as covering "every shared singleton" in the existing DI graph (`AiExecutionService`, behaviors, `InMemorySessionStore`, and any other singleton). Audit findings:

| Singleton | Finding | Disposition |
|---|---|---|
| `AiExecutionService`'s default `InMemoryEvidenceSink._records` (`Kernel.Evidence`) | Plain `List<EvidenceRecord>`; `Add`/enumeration are not thread-safe. Every `AiExecutionService.Execute` call on this process-wide singleton calls `_evidenceSink.Record(...)`, so concurrent adapter requests calling into AI-backed behaviors race on this list — can corrupt internal state or throw. | **Fix**: guard `_records` with a private `lock` on both `Record` and the `Records` getter (returns a defensive copy). Minimal, localized, no shape change to `IEvidenceSink` or `EvidenceRecord`. |
| `InMemorySessionStore.Touch` (`Runtime.Agent.Sessions`) | Non-atomic read-then-write on a `ConcurrentDictionary<string, ExecutionSession>`: reads the existing session, builds an updated record, then writes it back as two separate operations. Two overlapping requests reusing the same `SessionId` can race — one update silently overwrites the other's `TurnCount`/payload merge. | **Fix**: rewrite via `ConcurrentDictionary.AddOrUpdate`, whose `updateValueFactory` runs atomically against the dictionary's current value (retried internally on conflict), eliminating the read-then-write gap. The unknown-session-id throw behavior is preserved via the `addValueFactory` branch. |
| `EngineeringPromptCatalog.Templates`, `EngineeringAgentCapabilityCatalog.Capabilities` | Both `private static readonly` collections, populated once at type-init and never mutated. | Safe as-is — immutable after construction. |
| `EngineeringExecutionPipeline`, `AgentExecutionService` | No mutable instance state; every field set once in the constructor and never reassigned; all per-call state is local to the method invocation. | Safe as-is. |
| `ConversationIntakeAndRoutingBehavior`, `ConversationHandoffAndFollowupBehavior` (the two behaviors on every single request's Routing/Handoff hot path) | No instance fields beyond a `private static readonly` lookup table; `Execute` is a pure function of its `RuntimeExecutionContext` argument. | Safe as-is. |
| Remaining `IBehavior` singletons registered in `Program.cs` | Follow the same stateless-per-call shape verified above (constructor-injected readonly dependencies, no mutation of instance state during `Execute`). | Safe as-is — this table is the audit's written finding for the full singleton set; no further code changes proposed. |

This closes Capability 1's Completion Gate requirement: "a written finding (safe as-is, or the specific fix it requires) for every shared singleton." Both fixes are correctness bugs under concurrent access, not architectural changes — matching the RC0/P4 precedent of fixing defects inside already-frozen code without redesigning it. No broader concurrency-hardening effort is undertaken; nothing beyond these two fixes is in scope.

## Testing Plan

- `TrustedDefaultSecurityEvaluatorTests`: `Evaluate` returns `IsAllowed: true` for an empty context, a context with claims, and a null-claims context — it never denies.
- `SecurityEvaluatorSubstitutabilityTests`: a test-only `DenyAllSecurityEvaluator : ISecurityEvaluator` (always `IsAllowed: false`) is registered in DI in place of `TrustedDefaultSecurityEvaluator`, and the resolved `ISecurityEvaluator` reflects the substitution — proving the seam is genuinely swappable via DI with no change to `Runtime.Agent` or any adapter-facing contract. Satisfies GA-3 §9's Security Seam Validated gate.
- `InMemoryEvidenceSinkTests` (regression): many threads calling `Record` concurrently (e.g. `Parallel.For`) leaves `Records.Count` exactly equal to the number of calls, with no exception — proves the race is closed.
- `InMemorySessionStoreTests` (regression): many threads calling `Touch` concurrently on the *same* session id leaves `TurnCount` exactly equal to the number of calls — proves no lost updates.
- Full existing suite (324+ tests through RC0/GA-1/GA-2) stays green — re-run as the final gate, unchanged by this Capability's additive files.

## Documentation Updates (Definition of Done)

- Architecture doc: new `Runtime.Agent.Security` seam, and the "adapters are `IHostedService`" hosting convention.
- Developer docs: how a future adapter registers itself (`AddHostedService<T>`), and how it must call `ISecurityEvaluator` before submitting an `ExecutionRequest`.
- No new governed manifest entries required — same precedent GA-1/GA-2 already set for implementation-only DTOs with no governed `PCT-`/`SCH-` counterpart.

## Review

- ✓ `Runtime.Agent` remains the sole public execution boundary — the new seam is an additive contract inside it, not a new execution path; nothing bypasses `IAgentExecutionInterface`.
- ✓ No existing GA-1/GA-2/`EngineeringExecutionPipeline` file is modified.
- ✓ ADR-0025's dependency direction is unaffected — this Capability changes no adapter (none exist yet) and adds no new project.
- ✓ Both concurrency fixes are minimal, localized, and test-covered; no data structure or contract redesign.
- ✓ Scope matches the GA-3 program document's own Capability 1 chartering exactly (adapter-hosting model, security seam, concurrency audit) — health/diagnostics is left entirely to Capability 4's own design, not anticipated here.
- ✓ The security seam's DI substitutability is demonstrated by a test, not merely asserted.
