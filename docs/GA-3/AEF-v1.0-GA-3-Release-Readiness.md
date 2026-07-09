# AEF v1.0 GA-3 (External Execution Channels) — Release Readiness Report

GA-3 gives `aef-platform` two independent, production-shaped execution channels (REST, CLI) over `Runtime.Agent`, built entirely on the frozen RC0/GA-1/GA-2 execution core, plus the operational hardening (health, logging, metrics, graceful shutdown) needed to run either channel with confidence. This report closes every gate in the GA-3 architecture document's own §9 Release Readiness checklist.

## Capability Matrix

| Capability | Status | Design | Implementation Plan |
|---|---|---|---|
| 1 — AEF Runtime Host | Complete, merged | `docs/superpowers/specs/2026-07-08-ga3-capability1-runtime-host-design.md` | `docs/superpowers/plans/2026-07-08-ga3-capability1-runtime-host-implementation-plan.md` |
| 2 — REST Adapter | Complete, merged | `docs/superpowers/specs/2026-07-08-ga3-capability2-rest-adapter-design.md` | `docs/superpowers/plans/2026-07-08-ga3-capability2-rest-adapter-implementation-plan.md` |
| 3 — CLI Adapter | Complete, merged | `docs/superpowers/specs/2026-07-08-ga3-capability3-cli-adapter-design.md` | `docs/superpowers/plans/2026-07-08-ga3-capability3-cli-adapter-implementation-plan.md` |
| 4 — Cross-Cutting Hardening | Complete, merged | `docs/superpowers/specs/2026-07-08-ga3-capability4-cross-cutting-hardening-design.md` | `docs/superpowers/plans/2026-07-08-ga3-capability4-cross-cutting-hardening-implementation-plan.md` |
| 5 — Operational Readiness | Complete (this report) | `docs/superpowers/specs/2026-07-08-ga3-capability5-operational-readiness-design.md` | `docs/superpowers/plans/2026-07-08-ga3-capability5-operational-readiness-implementation-plan.md` |

Every Capability followed the same six-step lifecycle (Design → Independent Engineering Review → Implementation Plan → Implementation → Engineering Review → Merge); every design received at least one Independent Engineering Review round before implementation began.

## GA-3 §9 Release Readiness — Gate by Gate

- ✅ **Architecture Complete** — the GA-3 architecture document and all five Capability design specs are approved (each independently reviewed, most with findings resolved before approval).
- ✅ **Channels Complete** — CLI and REST are both implemented, each meeting its own Capability's completion gate. Contract compatibility: `tests/Adapters.Rest.Tests/ContractCompatibilityTests.cs` (property-name and `JsonValueKind` assertions against the real, live-serialized JSON for `ExecutionResponse`, `CapabilityDescriptor`, `ExecutionSession`). Concurrent-load: `tests/Adapters.Rest.Tests/ConcurrentExecutionTests.cs` (20 real concurrent HTTP requests, both without and with a shared session) — this test found and closed a real gap left open since Capability 2 (see "Defects Found and Fixed" below).
- ✅ **Documentation Complete** — Architecture (GA-3 doc + 5 Capability designs), Developer Guide, CLI Guide (`docs/cli/cli-adapter-reference.md`), REST Guide (`docs/api/rest-adapter-reference.md`), Operations Guide, Deployment Guide — all present under `docs/guides/`, `docs/cli/`, `docs/api/`.
- ✅ **Performance Validated** — see "Performance" below. Every GA-3 §8 metric measured and recorded; the one genuinely new risk (concurrency) is resolved, not merely documented (two real races found and fixed — see below).
- ✅ **Regression Complete** — 471/471 tests passing (see "Test Summary").
- ✅ **Engineering Execution Validated** — real, non-simulated Repository Intelligence requests executed end-to-end through both CLI and REST against the external `C:\POC\Ferret` repository (mirroring RC0 P4's own dogfooding precedent), plus a real concurrent-request scenario over REST. See "Dogfooding" below.
- ✅ **Security Seam Validated** — the trusted-default `ISecurityEvaluator` implementation works and is proven substitutable via DI (Capability 1's `GA3Capability1RuntimeHostTests.StartAsync_SecurityEvaluatorIsSubstitutableViaDI`, Capability 2's `ExecutionEndpointTests.PostExecution_SecurityDenies_ReturnsForbiddenAndNeverCallsRuntimeAgent`), not merely asserted in documentation.

## Dogfooding (Real, Non-Simulated)

Mirroring RC0 P4's own precedent: temporary, never-committed xUnit tests reusing this program's own established test harnesses (`CliExecutableFixture` for CLI, `WebApplicationFactory<Program>` for REST), run once against the real external `C:\POC\Ferret` repository, output captured verbatim, then deleted before the final commit (`git status` confirmed clean afterward).

**Request** (identical shape through both channels): `UserRequest: "Analyze this repository's architecture and code quality signals."`, `Payload: { RepositoryName: "Ferret", RepositoryRoot: "C:/POC/Ferret" }`.

**CLI result** (`AefPlatform.Cli.dll execute ... --output json`):
```json
{"requestId":"dc0c3da55fbd49e4893f26b00b8927e7","sessionId":null,"status":0,"error":null,"presentedResult":"Completed via WFL-DEL-0008.","suggestedNextStep":"No further action needed.","evidence":{"diagnostics":["[Unknown] Repository Understanding: No top-level or one-level-nested entry matching 'Analyze this repository's architecture and code quality signals.' was found.","[Pass] Test Convention: A conventional test directory was found at the repository root.","[Unknown] Code Intelligence: No Technology-Profile-resolved Automation exists in this Runtime for technical debt, dead code, duplication, complexity, or anti-pattern detection - reported UNKNOWN rather than fabricated, per AGT-EXE-0015's own Role #3."],"validationResults":["Could not locate 'Analyze this repository's architecture and code quality signals.' within the scanned bound."],"repositoryAnalyzed":"Ferret","filesChanged":[],"testsExecuted":null},"metadata":{"executedCapabilityId":"WFL-DEL-0008","executedAgentId":"AGT-EXE-0015","duration":"00:00:00.6089154", ...}}
```

**REST result** (`POST /api/v1/execution`): identical shape and content (`Status: Completed`, `executedCapabilityId: WFL-DEL-0008`, duration `602ms`) — both channels honestly report `[Unknown]` where no NL/LLM capability exists (no fabrication) and a real, evidence-derived `[Pass]` for the conventional test directory — the same "honest, evidence-grounded findings, never fabricated" standard RC0's own report held itself to.

**This dogfooding run initially failed on both channels** — see "Defects Found and Fixed" below; the results above are from the *fixed* code.

## Defects Found and Fixed (Execution-Discovered, This Capability)

Real dogfooding and real performance measurement — not toy requests — found four genuine defects in already-merged code, none hypothetical. Each was verified live, root-caused, classified, and fixed as a localized correctness repair (Founder-authorized in each case, per the standing governance: "verify the root cause, classify the defect, determine whether it is a localized correctness repair or a genuine architectural issue, escalate only when the fix would alter architecture, public behavior, or governance"). None altered any public contract, architecture, or behavior beyond closing the demonstrated defect.

1. **Concurrency — `ExecutionContextManager` and `StrategyHost`** (`src/Runtime/Context/ExecutionContextManager.cs`, `src/Runtime/Hosts/Strategy/StrategyHost.cs`). Both used a plain, non-thread-safe `Dictionary` mutated without synchronization — undiscovered until Capability 2 (REST) gave the Runtime its first concurrent callers, since Capability 1's own concurrency audit only covered `AiExecutionService`/`InMemorySessionStore`/`InMemoryEvidenceSink` and the routing/handoff behaviors, not the deeper Strategy/Context machinery the Feature Lifecycle Orchestrator uses. Reproduced live: 20 concurrent `POST /api/v1/execution` calls threw `InvalidOperationException: Operations that change non-concurrent collections must have exclusive access.` Fixed via `ConcurrentDictionary` (identical pattern to Capability 1's own `InMemorySessionStore` fix). Regression: `ExecutionContextManagerUnitTests.CreateContext_ConcurrentCalls_NoCorruption`, `StrategyHostUnitTests.Activate_ConcurrentCalls_NoCorruption`, and the real-HTTP-level `ConcurrentExecutionTests`.
2. **Adapter translation — `JsonElement` payload values** (new `src/Runtime.Agent/PayloadNormalization.cs`, consumed by both `Adapters.Rest` and `Adapters.Cli`). Both REST's `[FromBody]` model binding and CLI's `JsonSerializer.Deserialize` call produced payload values as boxed `JsonElement`, which `EngineeringExecutionPipeline.TryGetRepositoryContext`'s `is string name` pattern match never matched — `RepositoryName`/`RepositoryRoot` silently looked "missing" on **every** real JSON-delivered request through **either** channel since Capability 2/3 shipped. This is why the dogfooding run above initially failed on both channels with "No repository context was supplied." Fixed by normalizing `JsonElement` values to plain CLR types in the adapter translation layer, before constructing `ExecutionRequest` — `EngineeringExecutionPipeline` itself is unchanged. Regression: `PayloadNormalizationTests` (unit), `RepositoryContextPayloadTests` in both `Adapters.Rest.Tests` and `Cli.Tests` (end-to-end, using a portable temp directory rather than the external `Ferret` path).
3. **Deployment — `Host`'s `ContentRootPath`** (`src/Host/Program.cs`). `WebApplication.CreateBuilder`'s default content root is the process's current working directory, not the executable's own directory — `appsettings.json`'s `Ai:OpenAi` section silently failed to bind, crashing with a `NullReferenceException` at first AI-provider use, whenever `AefPlatform.Host.dll` was launched from anywhere other than its own output directory. Identical root cause (and fix) to `Cli`'s own `ContentRootPath` issue already fixed in Capability 3. Execution-discovered via live cold-boot performance measurement. Fixed by pinning `ContentRootPath` to `AppContext.BaseDirectory`. Regression: `HostExecutableStartupTests` (launches the real built executable from both its own directory and an arbitrary one).
4. **Test infrastructure — `CliExecutableFixture`'s argument quoting** (`tests/Cli.Tests/CliExecutableFixture.cs`). A manually quoted process-arguments string corrupted any argument containing an embedded double quote (e.g. a JSON payload) — found while adding the regression test for defect #2. Fixed by switching to `ProcessStartInfo.ArgumentList`, which the platform escapes correctly. Not a production defect; test tooling only.

## Performance (Against RC0's Baseline)

| Metric | RC0 baseline | GA-3 (measured live) |
|---|---|---|
| Cold boot (Host) | ~1.0–1.1s | ~1.16s |
| CLI per-invocation startup | n/a (no CLI existed) | ~0.30s |
| Idle working set (Host) | ~41.5MB | ~77.5MB (REST/health/logging/metrics infrastructure added since RC0) |
| Repository Intelligence request, real repo (`Ferret`) | 533–711ms | 602–608ms (CLI and REST) |
| Deterministic Feature Lifecycle request (REST, no AI) | not measured at RC0 | ~3–23ms steady-state (first request ~96ms, JIT warmup) |
| Concurrency (the one genuinely new risk per GA-3 §8) | n/a (single-caller only) | **Resolved** — two real races found and fixed (see above), verified under real concurrent HTTP load |

No regression beyond what the added surface area accounts for. Recovery (process-restart-on-crash) remains a deployment decision, per GA-3 §8's own framing — not built.

## Test Summary

**471/471 passing, 0 warnings** across all 8 test projects (`Kernel.Tests` 50, `Runtime.Ai.Tests` 25, `Runtime.Agent.Tests` 32, `Adapters.Cli.Tests` 17, `Cli.Tests` 6, `Adapters.Rest.Tests` 29, `Host.Tests` 39, `Runtime.Tests` 273), verified green on repeated consecutive runs.

## Known Limitations (Carried Forward Honestly, Not Silently Dropped)

- No real authentication/authorization — trusted-boundary assumption (Deployment Guide); the seam exists and is proven substitutable.
- No metrics vendor, no distributed tracing — `System.Diagnostics.Metrics` is vendor-neutral by design; attaching a listener is a deployment decision.
- No `RuntimeRegistryHealthContributor` — deliberately not built (Capability 4); would be trivially always-`Healthy` under the default `FailFastOnRegistrationError: true`.
- REST's `/api/v1/execution` error-mapping table treats most `Failed` outcomes as `200 OK` (Capability 2's own deliberate design — HTTP status is transport-level, not business-level; the caller inspects the body's `Status` field).
- No process-restart-on-crash mechanism — a deployment decision (container orchestrator, `systemd`, or equivalent).
- Idle working set grew from RC0's ~41.5MB to ~77.5MB — expected given REST hosting, health, structured logging, and metrics infrastructure did not exist at RC0; not investigated further as a regression, since GA-3 §8 only asks that growth be accounted for, not zero.
