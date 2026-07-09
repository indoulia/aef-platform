# Epic 3 — Repository Migration & Modernization: `aef-platform` Realization

Founder Execution Authorization granted for Epic 3 following the state-reconciliation report and pre-implementation verification (2026-07-08/09 session). This closes the gap the reconciliation found: Epic 3's Domain Content (`AGT-EXE-0016`, `SKL-EXE-0034`/`0035`/`0036`, `WFL-DEL-0009`, `CAP-DEL-0005`) was already authored and `Active` in `ai-engineering-framework`; none of it had a real `aef-platform` realization. This report closes that gap.

## What was built

`AGT-EXE-0016` Migration Planning Agent, dispatched via `WFL-DEL-0009`, composed of three Skills — mirroring `AGT-EXE-0012` Engineering Planner's own three-Skill shape, the closest existing precedent:

- **`SKL-EXE-0034`** (`MigrationAssessmentAndTransformationClassificationBehavior`) — classifies a migration goal into one of SCH-DEL-0003's six `Transformation Classification` values via deterministic keyword matching (the same house convention `ConversationIntakeAndRoutingBehavior` already established for cheap, low-ambiguity decisions), or asks for clarification rather than guessing. Current State is read from the already-detected Technology Profile (consumed, never re-derived); Target State always requires human input.
- **`SKL-EXE-0035`** (`MigrationStrategyPhasingAndRiskAnalysisBehavior`) — produces a concrete Phase 1 with a rollback boundary (mirroring `STD-DEL-0002`'s one-branch-one-PR-per-Story discipline); phasing beyond Phase 1, dependency/breaking-change analysis, and compatibility analysis are explicitly deferred to human input, never fabricated.
- **`SKL-EXE-0036`** (`MigrationSpecificationAssemblyAndApprovalRequestBehavior`) — assembles a `MigrationSpecification` (SCH-DEL-0003 `Specification Type: Migration`), decomposes it into ordinary Stories (one per phase), and requests Business Approval. `Status` is always `pending_approval` — mirrors `ReadinessClassificationAndApprovalRequestBehavior`'s own postcondition; this Behavior never self-renders the approval decision.

**Composition choice** (left open by the design, "left to whichever Epic actually builds this"): Migration Planning assembles its own `MigrationSpecification` independently rather than modifying Engineering Planner's `SpecificationDraft`. This touches zero of Engineering Planning's already-frozen, tested code — the Minimum Governance Change per `PRN-GOV-0006`.

**Wiring:** `FromMigrationPlanning` dispatch in `EngineeringExecutionPipeline`; registered in `EngineeringArchitectureManifest` (Registry Integrity), `EngineeringAgentCapabilityCatalog` (now 9 capabilities, up from 8), `ConversationIntakeAndRoutingBehavior` (`migrate`/`migration`/`modernize` routing keywords), and both `Cli`/`Host` `Program.cs` behavior registrations.

**Schema/Review Target compatibility confirmed, not assumed:** `ReviewTarget` (`EngineeringReviewInstance`) is an open `string` field, not a closed enum — the design's own "amends `SCH-DEL-0004`... `Review Target: Migration Completion`" item required **zero code change**; any future `AGT-EXE-0014` behavior can already construct a `Migration Completion`-targeted `REV` instance without a Runtime change.

## Defects found and fixed (execution-discovered, this Epic)

1. **Test infrastructure — the same flaky temp-git-repo cleanup race, reproduced in two more files.** The Windows file-lock race first found and fixed in `Host.Tests/RepositoryEngineeringHostIntegrationTests.cs` during the preceding reliability sweep reproduced live during this Epic's own full-suite verification, in `Runtime.Tests/Execution/EngineeringExecutionPipelineTests.cs` and `Runtime.Tests/Execution/RepositoryContextFactoryTests.cs` — exactly the residual risk flagged (not speculatively fixed) in that sweep's own closure report. Same bounded-retry fix applied to both. `Runtime.Tests/Integration/GitOperations/LocalGitOperationsTests.cs` already carried its own equivalent fix — left untouched. Two other files sharing `Directory.Delete(root, true)` (`CodeIntelligenceAndQualitySignalDetectionBehaviorTests`, `RepositoryAndArchitectureUnderstandingBehaviorTests`) do not invoke `git` and were not touched — no evidence of risk there, per "prove it, don't speculate."
2. **Test infrastructure — six pre-existing tests hardcoded the exact capability count (8).** Adding a ninth, real capability is exactly the kind of change these tests exist to catch; updated all six (`EngineeringAgentCapabilityCatalogTests`, `AgentExecutionServiceTests`, `CapabilitiesCommandTests` ×2, `GA2AgentExecutionHostTests`, `CapabilitiesEndpointTests`) to assert 9, confirmed via a real routing/dispatch/REST/CLI walkthrough that the count is genuinely 9, not a miscount.

Neither is a defect in Epic 3's own new code — both are pre-existing test-suite maintenance, surfaced by this Epic's own arrival, same as every prior Epic's own experience adding a capability.

## Regression tests added

18 new unit tests (`MigrationAssessmentAndTransformationClassificationBehaviorTests` ×7, `MigrationStrategyPhasingAndRiskAnalysisBehaviorTests` ×4, `MigrationSpecificationAssemblyAndApprovalRequestBehaviorTests` ×7) plus the 6 capability-count test updates above.

## Dogfooding (real, non-simulated)

Real requests through both REST and CLI against the real external `C:\POC\Ferret` repository:

- `"Migrate this repository to upgrade its target framework"` → classified `Technology/Framework Upgrade` (the `upgrade` keyword), `Completed` via `WFL-DEL-0009`/`AGT-EXE-0016`, identical shape and diagnostics on both channels.
- `"This repository needs a migration"` (genuinely ambiguous — routes on the `migration` noun but matches no classification keyword) → correctly asks *"Which transformation category applies?"* rather than guessing.
- Missing repository payload → clean clarification, no crash.
- 20 concurrent migration requests against the shared Ferret payload → 20 unique `requestId`s, zero corruption, `200` throughout.

## Test Summary

**498/498 passing, 0 warnings**, 3 consecutive clean full-suite runs (`Kernel.Tests` 50, `Runtime.Ai.Tests` 25, `Runtime.Agent.Tests` 32, `Adapters.Cli.Tests` 21, `Cli.Tests` 9, `Adapters.Rest.Tests` 29, `Host.Tests` 39, `Runtime.Tests` 293) — up from 480/480 before this Epic.

## Known limitations (carried forward honestly)

- Target State, phasing beyond Phase 1, dependency/breaking-change analysis, and compatibility analysis all currently and unconditionally require human input — by design (this capability's own Non-Goals: "not a code-transformation engine... a reviewed plan, not an unattended bulk rewrite"), meaning `ImplementationReadiness` currently never reaches `"Ready for Implementation"` automatically for any real request. This is the intended behavior for an initial construction pass, not a defect; a future iteration could narrow it with real evidence of what's safely automatable, if ever justified.
- No real multi-Story migration has been carried through actual completion (no real migration-shaped Feature/Bug exists yet in either repository to dogfood that end-to-end) — mirrors Repository Intelligence's (Epic 2) own single-real-request dogfooding scope, not a full multi-week migration cycle.
- CU placement (CU-5 vs. a future CU-6) remains an open planning-taxonomy question, explicitly out of this construction's scope per Founder instruction.
