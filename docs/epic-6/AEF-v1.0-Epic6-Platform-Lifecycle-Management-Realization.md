# Epic 6 — Platform Lifecycle Management: `aef-platform` Realization

Founder-directed continuation of Phase 2 dogfooding, following Epic 3/5's own realization pattern (2026-07-09 session): `AGT-EXE-0019` Platform Lifecycle Agent's Domain Content was already authored and `Active` in `ai-engineering-framework` (`epics/0013-platform-lifecycle-management.md`); none of it had a real `aef-platform` realization. This report closes that gap — the next incomplete roadmap item after Epic 5, per `ADR-0024`'s authorized order (Epics 2, 3, 5, 6 immediately; 4 after 2+3).

## What was built

`AGT-EXE-0019` Platform Lifecycle Agent, dispatched via `WFL-DEL-0012`, composed of five Skills. Unlike Migration/Maintenance's uniform 3-skill linear pipelines, this Agent is heterogeneous by design — its own six operation types (Installation, Upgrade, Repair, Reset, Backup, Restore) resolve through genuinely different mechanisms, matching the source design's own "classify-and-delegate for four of six" framing:

- **`SKL-EXE-0045`** (`PlatformLifecycleRequestClassificationBehavior`) — classifies the six operation types via a word-boundary-safe deterministic keyword classifier (needed because naive substring matching would match `install` inside `installation`).
- **Upgrade — real delegation, not reimplementation.** `EngineeringExecutionPipeline.FromPlatformLifecycle` invokes the already-merged Migration Agent's own Strategy/Assembly Skills (`SKL-EXE-0035`/`SKL-EXE-0036`) directly, constructing a `TransformationClassificationResult` in-line with `TransformationClassification` forced to `"Technology/Framework Upgrade"` — Migration's own classification Skill (`SKL-EXE-0034`) is deliberately skipped, since Platform Lifecycle's own classification already determined the operation type (Responsibility 3's own postcondition). Zero duplicated Migration logic.
- **Installation — honest non-invocation.** Real onboarding machinery exists in `src/Runtime/DomainContent/Onboarding/` (`RepositoryOnboardingAssessmentAgent`, `ProjectRegistrationBehavior`, `BaselineQualityGateBehavior`) but is confirmed (grep, not assumed) to have zero live dispatch entry on `EngineeringExecutionPipeline` or either Host/Cli `SkillHost` behavior map. Rather than wiring unrelated, previously-frozen onboarding code into this Epic's own scope, Installation performs its own narrow value-add (a readiness check) and names CU-3's onboarding sequence as the next step via an explicit OpenItem — never fabricating an invocation that doesn't exist.
- **`SKL-EXE-0046`** (`RegistryAndReferenceIntegrityDiagnosisBehavior`, Repair) — the one genuinely new diagnostic the design owns. Scans every `*.md` file under a repository root for `Id:`/`Depends On:`/`Used By:`/`Supersedes:` frontmatter lines and resolves each cited token against a declared `Id:` elsewhere in the scan, or (for path-shaped tokens) real file existence — cited by Id and file:line, never a fabricated PASS. This is real, working, read-only text analysis (Finding F5's file-write constraint does not apply), unlike Maintenance's registry-comparison dimensions, which had no realizable Automation at all.
- **`SKL-EXE-0047`** (`ArtifactPreservationClassificationBehavior`, Reset) — the design's own narrow finding: everything classifies `Governed` (never safe to discard) except a scope naming the Runtime Catalog, the one real `Derived Projection` in this repository.
- **`SKL-EXE-0048`** (`SnapshotAndRestorePlanningBehavior`, Backup & Restore) — Backup names the repository's already-known current branch as a candidate ref (real, read-only signal — no tag/ref is created); Restore names a target ref and the required post-restore consistency check, never invoking it during planning.
- **`SKL-EXE-0049`** (`PlatformLifecycleOperationPlanAssemblyAndApprovalRequestBehavior`) — assembles the `PlatformLifecycleOperationPlan`, classifies Implementation Readiness, and informationally names the two new `POL-GOV-0002` Escalation tiers (Founder-level for self-Upgrade; Human Approval for Reset/Restore beyond the Runtime Catalog projection). `Status` is always `pending_approval`.

**Wiring:** `FromPlatformLifecycle` dispatch in `EngineeringExecutionPipeline`; registered in `EngineeringArchitectureManifest`, `EngineeringAgentCapabilityCatalog` (now 11 capabilities, up from 10), `ConversationIntakeAndRoutingBehavior`, and both `Cli`/`Host` `Program.cs` behavior registrations.

## Defects found and fixed (execution-discovered, this Epic)

An independent code-quality-auditor subagent, given zero prior context, found and this session fixed:

1. **HIGH — path traversal in the Repair diagnostic.** `RegistryAndReferenceIntegrityDiagnosisBehavior`'s path-shaped-token resolution used `File.Exists(Path.Combine(root, token))` directly on untrusted content scanned from Markdown frontmatter — an absolute-path token (`Depends On: [C:\Windows\...]`) bypassed `root` entirely (`Path.Combine` returns a rooted second argument unmodified), and a `../../` token walked out of the repository root before resolution. Fixed by canonicalizing with `Path.GetFullPath` and requiring the result to stay within a trailing-separator-qualified canonical root before calling `File.Exists`; two regression tests added (absolute-path and traversal tokens, both now correctly classify `FAIL`).
2. **MEDIUM — inconsistent routing-keyword coverage.** Only `repair` got a `"my aef"` variant; the other five verbs didn't, and separately, conversation-level routing initially required `"aef"` in the phrase for every verb — breaking the design's own literal worked examples (`"Reset generated artifacts"`, `"Rollback an upgrade"`, neither of which says "AEF"). Fixed: only `upgrade` stays AEF-qualified (the one verb that would otherwise collide with Migration's/Maintenance's own upgrade-shaped requests, neither of which claims the word today but both of which plausibly could); the other five verbs (`install`/`repair`/`reset`/`backup`/`restore`/`rollback`) route bare, since none collides with any existing keyword.
3. **LOW — a latent OpenItems-chain inconsistency** in the Upgrade delegation (`forcedClassification.OpenItems` was dropped rather than concatenated before Assembly, matching Migration's own `inheritedOpenItems` pattern). Harmless today (always empty), fixed for correctness-by-construction rather than left as a footgun.

## Test infrastructure maintenance (pre-existing, surfaced by this Epic's own arrival)

Six pre-existing tests hardcoded the exact capability count (10). Updated all six to assert 11, confirmed via a real routing/dispatch/REST/CLI walkthrough.

## Known pre-existing flake (not fixed — same class already documented in Epic 5)

One full-suite run during this Epic's own verification hit the identical, already-diagnosed Windows temp-git-repo cleanup race (this time in `Host.Tests`) — reproduces only under full-suite parallel contention, unrelated to Platform Lifecycle. Three subsequent full-suite runs were clean. Left undisturbed per the same reasoning recorded in Epic 5's own realization report.

## Regression tests added

30 new unit tests across the five new Behaviors, plus 2 security-regression tests added during review, plus 6 new routing-keyword test cases, plus the 6 capability-count test updates above.

## Dogfooding (real, non-simulated)

Real requests through both REST and CLI:

- **Upgrade** (`"Upgrade AEF from v1.0 to v1.1"` against `C:\POC\Ferret`) — real delegation confirmed: the response's diagnostics are Migration's own Strategy Skill's exact output text ("Phasing beyond Phase 1 requires human input...", etc.), not fabricated Platform-Lifecycle-specific text.
- **Repair** (`"Repair my AEF installation"` against `C:\POC\ai-engineering-framework`, its own real governed docs) — a standalone reproduction of the exact scan logic against this real 432-file, 227-declared-Id repository found 536 real reference citations (225 unique declared Ids after de-duplication); confirmed the Behavior's own logic is correct. The scan's own findings are computed into the Plan's `Details` field but not surfaced in the CLI/REST response's top-level `diagnostics` — a pre-existing limitation shared by every sibling capability (`ConversationHandoffAndFollowupBehavior`, `SKL-EXE-0022`, always overwrites `PresentedResult`/`SuggestedNextStep` with a generic `"Completed via {capabilityId}."`, regardless of which capability ran), not something this Epic introduced or could fix within its own scope.
- **Installation** (`"Install AEF into this repository"`) — honest OpenItem naming CU-3's own unwired onboarding sequence, never a fabricated invocation.
- **Reset** — `"Reset the Runtime Catalog projection"` → `Derived Projection`; `"Reset generated artifacts"` → `Governed`; both `Ready for Implementation` (zero OpenItems).
- **Backup** (`"Backup AEF now"` against `C:\POC\aef-platform` itself, mid-session, with real uncommitted Epic 6 changes) — correctly named the current branch as the candidate ref **and** correctly flagged `"Working tree is not clean"` with the real list of this session's own changed files as `filesChanged` — a genuinely live, self-referential proof.
- **Restore** (with a supplied target ref) — named the ref and the required post-restore consistency check, never executed it.
- Missing repository payload, both channels → clean clarification, no crash.
- `GET /api/v1/capabilities` and `aef-cli capabilities` both list 11 capabilities including `WFL-DEL-0012`, identical shape on both channels.

## Test Summary

**553/553 passing** (up from 515/515 before this Epic — Epic 5 landed at 515, not 498; corrected here), 0 warnings, 3 consecutive clean full-solution runs. `Kernel.Tests` 50, `Runtime.Ai.Tests` 25, `Runtime.Agent.Tests` 32, `Adapters.Cli.Tests` 21, `Cli.Tests` 9, `Adapters.Rest.Tests` 29, `Host.Tests` 39, `Runtime.Tests` 348.

## Known limitations (carried forward honestly)

- Repair's Plan findings (Details) are computed correctly but not surfaced in the final `ExecutionResult` — the shared Handoff overwrite limitation named above. A future capability could add a `Plan`/rich-detail field to `ExecutionResult` if real evidence shows this is needed; not attempted speculatively here.
- Installation never actually invokes CU-3's onboarding sequence — it has no live dispatch entry on this Pipeline; wiring it is a larger, cross-cutting change (touching previously-frozen Onboarding domain content) explicitly out of this Epic's own scope.
- Repair's "Registry registration" check is not performed — the Runtime Registry's own query surface is single-Id-lookup-only (no enumerate-all), a real, named, inherited limitation this design's own Reuse Ledger already carries, not a shortcut taken here.
- No real Platform Lifecycle operation has been carried through actual human-directed execution (no real AEF upgrade/repair/backup has ever been performed) — mirrors every sibling Epic's own single-real-request dogfooding scope.
