# GA-3 Capability 5: Operational Readiness — Design

**Status:** Independent Engineering Review complete (Approved with minor findings, all resolved): corrected the gate-disposition table's attribution (Decision 2, not Decision 1, retroactively closes Capability 2's own unmet concurrent-load completion gate), stated explicitly how `TurnCount` is observed in the concurrent-session test, justified the "20 concurrent requests" figure, disclosed the contract test's residual type-change gap (and added `JsonValueKind` assertions to close it), and strengthened the dogfooding methodology to match RC0's own structured, assertion-backed rigor rather than an ad hoc substitute. Ready for the Implementation Plan. Cites `docs/superpowers/specs/2026-07-08-ga3-external-execution-channels-engineering-program.md` (GA-3, especially its §7 Documentation, §8 Performance, and §9 Release Readiness) and Capabilities 1–4's design docs rather than re-deriving them.

## Mission

GA-3 §4 Capability 5: "the release gate itself, detailed in Section 9." This document closes every remaining item in GA-3 §9's Release Readiness checklist that Capabilities 1–4 did not already close as a byproduct of their own work: the REST contract-compatibility test, a real concurrent-load test resolving Capability 1's concurrency audit "not just documented as a risk," a real (non-simulated) dogfooding run through both CLI and REST against an external repository (mirroring RC0 P4's own precedent), measured-and-recorded performance figures against RC0's baseline, and the remaining documentation artifacts GA-3 §7 names. No new execution capability, no new endpoint, no new command — this Capability validates and documents what Capabilities 1–4 already built.

## Frozen Inputs (not redesigned by this document)

RC0, `Runtime.Ai`, `Runtime.Agent`'s existing contracts, `EngineeringExecutionPipeline`, ADR-0025, Capabilities 1–4 in full (Runtime Host, REST Adapter, CLI Adapter, Cross-Cutting Hardening). This document adds tests and documentation; it does not change any existing endpoint, command, contract, or configuration default.

## Non-Goals

New execution features, new endpoints, new commands, new configuration options, a real APM/metrics vendor integration, real authentication, and — per GA-3 §8's own "Recovery" row — a process-restart-on-crash mechanism ("a deployment decision... not a new mechanism this program builds").

## GA-3 §9 Release Readiness — Gate-by-Gate Disposition

| Gate | Status before this Capability | This Capability's action |
|---|---|---|
| Architecture Complete | ✓ — GA-3 doc + all four Capability design docs approved | None needed; recorded in the final report. |
| Channels Complete | Partial — CLI and REST both implemented and tested, but no contract-compatibility test exists yet, **and** Capability 2's own Completion Gate ("concurrent requests behave correctly under a real concurrent-load test") was never actually closed at the time (its Testing Plan was sequential-only) | Decision 1 (contract test) **and** Decision 2 (retroactively closes Capability 2's own unmet concurrent-load completion gate — corrected attribution per Independent Engineering Review) |
| Documentation Complete | Partial — Architecture, CLI Guide (`docs/cli/cli-adapter-reference.md`), REST Guide (`docs/api/rest-adapter-reference.md`) exist; Developer Guide, Operations Guide, Deployment Guide do not | Decision 5 |
| Performance Validated | Not started — no figures measured against RC0's baseline since GA-3 began | Decision 4 |
| Regression Complete | ✓ — 453/453 passing continuously through Capability 4 | Re-verified as the final gate (Task in the plan); recorded in the report. |
| Engineering Execution Validated | Not started — no real (non-simulated), non-toy request has been run through either channel against an external repository | Decision 3 |
| Security Seam Validated | ✓ — Capability 2's `PostExecution_SecurityDenies_ReturnsForbiddenAndNeverCallsRuntimeAgent` already proves substitutability | None needed; recorded in the final report. |

## Decision 1 — REST Contract-Compatibility Test

GA-3 §5's Contract Compatibility row: "a compatibility policy... and a contract test enforcing it are both required, not just the version segment in the URL." New `tests/Adapters.Rest.Tests/ContractCompatibilityTests.cs`: serializes a representative `ExecutionResponse`, `CapabilityDescriptor`, and `ExecutionSession` and asserts the exact set of JSON property names present (camelCase, per Capability 2's own convention) — a snapshot-style regression test. If a future change to any of these types (all frozen GA-2 contracts, so unlikely to change, but the *serialization* of them by the REST layer is this Capability's own surface) silently adds, removes, or renames a JSON property, this test fails immediately rather than the drift being discovered by an external consumer. This is deliberately a property-name/shape test, not a full JSON-schema validator — proportionate to "one real contract test," not a new schema-validation framework (no new dependency). **Residual gap, disclosed rather than assumed away:** a property-presence test does not catch a breaking *type* change on an existing property (e.g. `status` silently becoming numeric instead of a string) — only additions, removals, and renames. Closing that residual gap would mean either hand-writing per-property type assertions (proportionate, could be added here) or a real JSON-schema library (a new dependency, out of scope); the implementation plan adds explicit `JsonValueKind` assertions per property (still no new dependency) to close this gap without escalating to a schema-validation framework.

## Decision 2 — Concurrent-Request Test (closes Capability 1's concurrency-audit gate for real)

Capability 1's concurrency audit fixed two real races (`InMemoryEvidenceSink`, `InMemorySessionStore.Touch`) but was verified only by targeted unit tests calling the fixed code directly with `Parallel.For` — never by real, concurrent HTTP requests through the actual REST pipeline. GA-3 §8: "verified again under Capability 2's completion gate with an actual concurrent-load test" — that test was never written; this Capability writes it. New `tests/Adapters.Rest.Tests/ConcurrentExecutionTests.cs`: fires 20 concurrent `POST /api/v1/execution` calls (via `WebApplicationFactory`'s real `TestServer`, which does dispatch concurrently in-process, unlike sequential `await` calls every other REST test in this suite uses) against the same deterministic, AI-free `"start a new feature"` request already used throughout Capabilities 2–4's own tests, asserting: every response is `200 OK` with `Status: Completed`, every `RequestId` echoed back is unique (proving no cross-request state bleed), and no exception/500 occurs. **Why 20:** not a load-test figure — chosen as a small multiple of the default .NET thread-pool's minimum worker-thread count on a typical development machine, large enough to force genuine interleaving of the `InMemoryEvidenceSink`/`InMemorySessionStore` singletons under `TestServer`'s real concurrent dispatch (which the earlier unit-level `Parallel.For` tests, run outside any HTTP pipeline, could not exercise), small enough to keep the test fast and deterministic — not a claim about production concurrency limits, which are a deployment decision (GA-3 §8's "Recovery" row precedent). A second variant repeats this with all 20 requests sharing one `SessionId` (obtained via a real `POST /api/v1/sessions` call first) — the scenario `InMemorySessionStore.Touch`'s fix specifically targets — asserting the final session's `TurnCount` equals 20 (no lost updates under real HTTP concurrency, not just the unit-level `Parallel.For` Capability 1 already proved). **How `TurnCount` is observed, stated explicitly:** Capability 2 deliberately has no `GET /api/v1/sessions/{id}` endpoint, so the test reads the final `ExecutionSession` directly from the `WebApplicationFactory`'s own DI container (`factory.Services.GetRequiredService<ISessionStore>().Get(sessionId)`) after all 20 HTTP calls complete — a test-only, in-process shortcut, not a claim that this value is reachable over HTTP today.

## Decision 3 — Real Dogfooding Through Both Channels (mirroring RC0 P4's precedent)

RC0 P4's own dogfooding ran real Repository Intelligence requests against the external `C:\POC\Ferret` repository via a **temporary, never-committed** xUnit test reusing `RepositoryEngineeringHostIntegrationTests`'s own established pattern — structured, programmatic, producing the itemized Success/routing/confidence evidence RC0's own report shows. This Capability matches that rigor exactly, not a lesser "curl it by hand" substitute (Independent Engineering Review correctly flagged the design's first draft as under-delivering here):

- **CLI**: a temporary, never-committed xUnit test in `tests/Cli.Tests` (same `CliExecutableFixture.Run` helper Capability 3 already established), invoking `execute --user-request "Analyze this repository's architecture and code quality signals." --project-profile-id default-profile --payload-json "{\"RepositoryName\":\"Ferret\",\"RepositoryRoot\":\"C:\\\\POC\\\\Ferret\"}"` against the real `C:\POC\Ferret` repository, asserting the real exit code and asserting the real stdout is non-fabricated (contains a genuine `[Unknown]`/honestly-low-confidence marker where no NL/LLM capability exists, exactly as RC0's own report documents, not a suspiciously complete/fabricated-looking analysis).
- **REST**: a temporary, never-committed xUnit test in `tests/Adapters.Rest.Tests` reusing the existing `WebApplicationFactory<Program>` fixture, `POST /api/v1/execution` with the equivalent JSON body against the real `C:\POC\Ferret` path, asserting the same real-response shape.

Both temporary test files are deleted (`git status` confirms nothing new remains in `tests/`) once their run's actual output is captured verbatim into the Release Readiness report (Decision 6) — matching RC0's own "temporary, never committed" discipline while now using the same structured, assertion-backed harness RC0 itself used, not a weaker ad hoc substitute.

Both runs' actual output (status, presented result, diagnostics) are captured verbatim into the Release Readiness report (Decision 6) — the same "honest, evidence-grounded findings, never fabricated" standard RC0's own report held itself to. This closes "Engineering Execution Validated... through both CLI and REST against a real repository" without adding a permanent, environment-coupled test to the CI-run suite.

## Decision 4 — Performance Measurement (against RC0's baseline)

RC0's measured baseline: ~1.0–1.1s cold boot, ~41.5MB idle working set, 533–711ms for a real Repository Intelligence request. GA-3 §8 asks these be re-measured, not re-derived from scratch, now that `Host` is long-running with REST/health/logging/metrics added and `Cli` is a second, separate process. Measured live (not asserted by an automated test — these are point-in-time, environment-sensitive figures, exactly how RC0's own report treated them) and recorded in the Release Readiness report:

- **Host cold-boot time**: wall-clock from process start to the first successful `GET /api/v1/health` response.
- **CLI per-invocation startup**: wall-clock for `capabilities` (the cheapest real command) end-to-end, since GA-3 §8 explicitly flags this as "a new cost REST doesn't have."
- **Host idle working set**: `Process.WorkingSet64` a few seconds after boot, no requests yet served.
- **REST adapter-layer latency overhead**: the same `"start a new feature"` request's REST round-trip time compared against RC0's own 533–711ms figure for a comparable (if not identical — RC0's was Repository Intelligence, this Capability's deterministic test path is Feature Lifecycle) in-process pipeline call, to characterize whether the adapter layer itself (JSON parsing, security seam, logging middleware) adds material overhead — not a strict apples-to-apples replay of RC0's exact request.

No specific pass/fail threshold is invented here beyond GA-3 §8's own qualitative bar ("should not regress materially") — these are recorded figures for the report, not new CI gates, consistent with how RC0's own Runtime Metrics table was populated.

## Decision 5 — Remaining Documentation

Three new documents, per GA-3 §7's remaining unstarted items:

- **`docs/guides/developer-guide.md`**: how to call `IAgentExecutionInterface` in-process (a GA-2 deliverable, never written down as its own guide — only ever described inline in the GA-2 design doc), plus how to run `AefPlatform.Host` locally (`dotnet run --project src/Host`) and `AefPlatform.Cli` locally.
- **`docs/guides/operations-guide.md`**: running the Host, the `/api/v1/health` signal and what `503` should prompt an operator to check (Capability 4), graceful shutdown behavior and `ShutdownTimeoutSeconds` (Capability 4), the structured request-log format and `dotnet-counters` metrics (Capability 4).
- **`docs/guides/deployment-guide.md`**: process/container expectations (a single `dotnet` process per adapter — `Host` for REST, `Cli` invoked per command), the full configuration surface (`appsettings.json`'s `AefPlatform`/`Ai`/`RestAdapter` sections, consolidated in one place for the first time), and the trusted-boundary assumption this GA operates under (no real authentication, no HTTPS termination configured — both explicit Non-Goals through Capability 4 — and what changes when a later GA revisits them).

## Decision 6 — The Consolidated Release Readiness Report

`docs/GA-3/AEF-v1.0-GA-3-Release-Readiness.md`, mirroring `docs/RC0/AEF-v1.0-RC0.md`'s own established structure and standard of evidence (Capability Matrix, Validation Report, Test Summary, Runtime Metrics, Known Limitations) — the closing artifact of this program, checking off every GA-3 §9 gate from the table above with a link to its evidence (a test file, a design doc, or this report's own recorded measurement/dogfooding section).

## Testing Plan

- `ContractCompatibilityTests` (Decision 1).
- `ConcurrentExecutionTests` (Decision 2) — both variants (unique-session-per-request, shared-session-across-requests).
- Dogfooding (Decision 3) is performed via temporary, never-committed xUnit tests during implementation (deleted before the final commit) — its output is evidence recorded in the report, per RC0's own precedent and matching RC0's own structured rigor, not a lesser ad hoc substitute.
- Performance measurement (Decision 4) is performed live during implementation and recorded in the report — not a committed test.
- Full existing suite (453 tests through Capability 4) stays green — re-run as the final gate, and its pass count is itself one line of evidence in the Release Readiness report.

## Documentation Updates (Definition of Done)

- `docs/guides/developer-guide.md`, `docs/guides/operations-guide.md`, `docs/guides/deployment-guide.md` (new, Decision 5).
- `docs/GA-3/AEF-v1.0-GA-3-Release-Readiness.md` (new, Decision 6).
- `README.md` Status section: final GA-3 entry recording the whole program's completion, not just this Capability.

## Review

- ✓ No existing endpoint, command, contract, or configuration default changes — this Capability is additive tests and documentation only.
- ✓ Dogfooding and performance measurement follow RC0 P4's own established precedent (live, recorded, not committed as environment-coupled CI tests) rather than inventing a new validation methodology.
- ✓ The concurrent-request test genuinely exercises Capability 1's fixed races through real HTTP concurrency, not a repeat of the existing unit-level `Parallel.For` tests.
- ✓ Every GA-3 §9 gate is traced to concrete evidence, not asserted narratively.
- ✓ No Non-Goal item (new features, new endpoints/commands, metrics vendor, real auth, process-restart mechanism) appears anywhere in this design.
