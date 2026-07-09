# Epic 5 — Engineering Maintenance & Repository Hygiene: `aef-platform` Realization

Founder-directed continuation of Phase 2 dogfooding, following Epic 3's own realization pattern (2026-07-09 session): `AGT-EXE-0018` Repository Maintenance Agent's Domain Content was already authored and `Active` in `ai-engineering-framework` (`epics/0010-engineering-maintenance-and-repository-hygiene.md`); none of it had a real `aef-platform` realization. This report closes that gap — the first incomplete roadmap item after Epic 3, per `ADR-0024`'s authorized order (Epics 2, 3, 5, 6 immediately; 4 after 2+3).

## What was built

`AGT-EXE-0018` Repository Maintenance Agent, dispatched via `WFL-DEL-0011`, composed of three Skills — mirroring `AGT-EXE-0016` Migration Planning Agent's own three-Skill shape, the closest existing precedent:

- **`SKL-EXE-0042`** (`RepositoryHealthAssessmentBehavior`) — classifies each of the design's eight Health dimensions (Dependency, SDK, Package, Configuration, Project Structure, Build, Documentation, Code Hygiene). No per-stack, registry-comparison Automation exists anywhere in this Runtime (confirmed: no `adapters/` directory has ever been materialized, the same honest gap the source design itself acknowledges) — every dimension therefore classifies `UNKNOWN` with a recorded rationale, reusing `SKL-EXE-0017`'s exception-recording discipline verbatim, never a fabricated `PASS`/`WARNING`/`FAIL` guess. The rationale cites the detected Technology Profile where one exists.
- **`SKL-EXE-0043`** (`MaintenancePlanningRiskAndSequencingBehavior`) — formulates one Maintenance Item per Health finding classified `WARNING`/`FAIL` (never `PASS`/`UNKNOWN`); Risk classification always defaults to the design's own conservative default, `Potentially breaking`, since no resolvable check exists to prove `Non-breaking`; Safe-to-Automate is always `false` for the same reason. Genuinely functional given real findings, not merely a pass-through — proven by unit tests constructing `WARNING`/`FAIL` findings directly, independent of what `SKL-EXE-0042` currently produces end-to-end.
- **`SKL-EXE-0044`** (`MaintenanceSpecificationAssemblyAndApprovalRequestBehavior`) — assembles a `MaintenanceSpecification` (`SCH-DEL-0003` `Specification Type: Maintenance`), decomposes it into one Story per Maintenance Item, and requests Business Approval. `Status` is always `pending_approval` — mirrors `MigrationSpecificationAssemblyAndApprovalRequestBehavior`'s own postcondition; this Behavior never self-renders the approval decision.

**Composition choice** (mirrors Migration's own precedent): Maintenance Planning assembles its own `MaintenanceSpecification` independently rather than modifying Engineering Planner's `SpecificationDraft`, touching zero of Engineering Planning's already-frozen, tested code.

**Wiring:** `FromMaintenancePlanning` dispatch in `EngineeringExecutionPipeline` (structurally identical to `FromMigrationPlanning`); registered in `EngineeringArchitectureManifest`, `EngineeringAgentCapabilityCatalog` (now 10 capabilities, up from 9), `ConversationIntakeAndRoutingBehavior` (`maintenance`/`maintain`/`hygiene` routing keywords), and both `Cli`/`Host` `Program.cs` behavior registrations.

## Defects found and fixed (execution-discovered, this Epic)

None in Epic 5's own new code. An independent code-quality-auditor subagent, given zero prior context and asked to verify line-by-line against the diff and the Migration sibling's own established pattern, found no correctness, security, resource, or consistency issues.

## Test infrastructure maintenance (pre-existing, surfaced by this Epic's own arrival)

Six pre-existing tests hardcoded the exact capability count (9). Adding a tenth, real capability is exactly the kind of change these tests exist to catch; updated all six (`EngineeringAgentCapabilityCatalogTests`, `AgentExecutionServiceTests`, `CapabilitiesCommandTests` ×2, `GA2AgentExecutionHostTests`, `CapabilitiesEndpointTests`) to assert 10, confirmed via a real routing/dispatch/REST/CLI walkthrough that the count is genuinely 10, not a miscount. Neither is a defect in Epic 5's own new code — pre-existing test-suite maintenance, the same experience every prior Epic adding a capability has had.

## Known pre-existing flake (not fixed — no new evidence beyond frequency)

`Runtime.Tests.Execution.EngineeringExecutionPipelineTests.Execute_ValidateRequest_RealTestFailure_ReportsFailure_NeverFabricatesASuccess` intermittently failed 3 of 9 full-suite runs during this Epic's own verification, always with an `IOException` inside `DeleteRepository`'s temp-git-repo cleanup — the identical Windows file-lock race already diagnosed and bounded-retried (5 attempts) during Epic 3's own dogfooding sweep, in this exact file and in `Host.Tests/RepositoryEngineeringHostIntegrationTests.cs`. Reproduces only under full-suite parallel contention — 6/6 clean in isolation, confirmed directly, not assumed. Unrelated to Repository Maintenance (the failing test exercises `REPO-VALIDATION`, not `WFL-DEL-0011`); left as a documented residual risk rather than speculatively re-tuning an already-bounded retry with no new evidence of a different root cause, per `PRN-GOV-0006`'s minimum-change discipline.

## Regression tests added

17 new unit tests (`RepositoryHealthAssessmentBehaviorTests` ×5, `MaintenancePlanningRiskAndSequencingBehaviorTests` ×7, `MaintenanceSpecificationAssemblyAndApprovalRequestBehaviorTests` ×5) plus the 6 capability-count test updates above.

## Dogfooding (real, non-simulated)

Real requests through both REST and CLI against the real external `C:\POC\Ferret` repository:

- `"Assess this repository's maintenance and hygiene"` / `"Assess this repository maintenance and hygiene"` → routes on the `maintenance`/`hygiene` keywords, `Completed` via `WFL-DEL-0011`/`AGT-EXE-0018` on both channels, identical response shape (`status`, `presentedResult`, `evidence.diagnostics`, `evidence.validationResults`, `metadata.executedAgentId`) on both. Every Health dimension resolves `UNKNOWN` (no Automation materialized), yielding zero Maintenance Items and `validationResults: ["Requires Clarification"]` — the honest, non-fabricated result this Runtime can currently produce.
- Missing repository payload, both channels → clean clarification (`No repository context was supplied...`), no crash, `status: Failed`.
- `GET /api/v1/capabilities` and `aef-cli capabilities` both list 10 capabilities including `WFL-DEL-0011`.

## Test Summary

**515/515 passing** (up from 498/498 before this Epic), 0 warnings. `Kernel.Tests` 50, `Runtime.Ai.Tests` 25, `Runtime.Agent.Tests` 32, `Adapters.Cli.Tests` 21, `Cli.Tests` 9, `Adapters.Rest.Tests` 29, `Host.Tests` 39, `Runtime.Tests` 310. Full-solution build: 0 warnings, 0 errors.

## Known limitations (carried forward honestly)

- Every Health dimension currently resolves `UNKNOWN` end-to-end, since no per-stack, registry-comparison Automation (the `AUT` concept's `adapters/<technology>/` realization) exists anywhere in this Runtime yet — the same pre-existing, shared gap every sibling design that reasons about per-stack tooling already carries. `SKL-EXE-0043`/`SKL-EXE-0044` are genuinely functional against real `WARNING`/`FAIL` findings (proven by unit test, not merely asserted) — they simply have nothing to act on until that Automation is built, by design, not as a shortcut taken here.
- No real Maintenance Item has been carried through actual implementation (no real dependency/SDK bump exists yet in either repository to dogfood end-to-end) — mirrors Repository Migration's (Epic 3) own identical single-real-request dogfooding scope.
- CU placement (CU-5 continuation vs. a future CU) remains an open planning-taxonomy question, explicitly out of this construction's scope, consistent with every sibling Epic's identical treatment.
