# P4 — Runtime Hardening, Dogfooding & RC0 Release

> **For agentic workers:** this document records what was actually done, not a forward-looking plan — P4 was executed autonomously end-to-end in one session under the delegated implementation authority already granted for Epic 1 (`ADR-0023`) and Phase 2 (`ADR-0024`).

**Goal:** Prove the already-content-complete P1/P2/P3 Runtime is stable enough for an AEF v1.0 Release Candidate — via real dogfooding against a genuine external repository (not a synthetic fixture), an independent defect audit of the riskiest runtime surfaces, and fixes for anything Critical/High found — without redesigning architecture, adding new Capability Units, or inventing new agents.

## Phase 1 — Readiness Assessment

Before any change: `dotnet build AefPlatform.sln` (0 warnings, 0 errors), `dotnet test AefPlatform.sln` (317/317 passing at the time: 45 Kernel, 21 Host, 251 Runtime), and a real Host boot (`dotnet run` from `src/Host`) confirming `Runtime Architecture Loader: 36/36 artifacts registered.` and clean graceful shutdown. All green — no regression to fix before proceeding.

## Phase 2 — Real Dogfooding (against Ferret)

Ferret (`C:\POC\Ferret`, a real, independently-maintained repository on `main`, clean working tree) was exercised through the real, booted `EngineeringExecutionPipeline` — the same composition point `Program.cs` wires at real boot — via a temporary (never committed) xUnit harness reusing `RepositoryEngineeringHostIntegrationTests`'s own established pattern, pointed at Ferret's real absolute path instead of a synthetic temp repo.

Three real requests were executed:
1. A broad natural-language request ("Analyze this repository's architecture and code quality signals.") — correctly reported `[Unknown]` for the literal-NL-sentence lookup (no NL/LLM capability exists, honestly reported rather than fabricated), while still surfacing a real, evidence-derived `[Pass]` finding (a conventional test directory exists at Ferret's real repository root).
2. A request scoped to a real top-level entry (`RequestScope="docs"`) — correctly found and reported Ferret's real `docs/` directory.
3. A diagnostics request (`ReportedProblem="CI pipeline runs are slow"`, `RequestScope="build"`) — correctly found Ferret's real `build/` directory and reported a fixed, honestly-justified low root-cause confidence score (`2/10`) rather than fabricating a causal conclusion, exactly matching `AGT-EXE-0015`'s own documented Role.

A fourth negative case (a request phrased without any `SKL-EXE-0021` routing keyword) correctly triggered a clarification request instead of guessing — proving the deterministic keyword router's own honest-failure discipline holds under a real, unscripted phrasing.

`REPO-VALIDATION` (build/test execution) was deliberately **not** run against Ferret's real files — it would invoke Ferret's actual, unbounded `dotnet`/`npm` build+test commands; that dispatch path is already proven for real (not synthetic) process execution via `RepositoryEngineeringHostIntegrationTests` against a fast, disposable temp repository.

**No defects were found during this exercise itself** — every real request either succeeded with a genuine, evidence-grounded finding or honestly declined rather than fabricate. See `p4-dogfooding-evidence.md`-equivalent detail folded into the RC0 package's Dogfooding Summary.

## Phase 3 — Runtime Stabilization

An independent code-quality audit (a fresh reviewer agent, given no prior context, instructed to verify every finding line-by-line against current source before reporting) targeted the areas most likely to hide real defects under the exact kind of load real dogfooding exercises: external process invocation, repository file/git operations, pipeline dispatch, and Kernel shared state under this long-running host's concurrency model.

**Confirmed and fixed (all with regression tests, verified against the actual current source before any code changed):**

| Severity | File | Defect | Fix |
|---|---|---|---|
| Critical | `LocalGitOperations.RunGit`, `ProcessVerificationRunner.Run` | Sequential `ReadToEnd()` on stdout then stderr is a classic `Process` deadlock once the un-drained stream fills its OS pipe buffer — realistic for `git diff` on a large change, or any real build/test command (exactly what `REPO-VALIDATION` runs in production) | Concurrent async reads (`ReadToEndAsync` for both streams before `WaitForExit`) + a bounded timeout with process-tree `Kill` on expiry |
| High | `InMemoryRegistry.Register`, `InstanceSupervisor.CreateInstance`/`DestroyInstance` | Plain `Dictionary` with a non-atomic check-then-act pattern — a real race under this long-running host's concurrent registration/activation traffic (two concurrent `Register` calls for the same Id could both "win", defeating the documented no-silent-overwrite guarantee) | `ConcurrentDictionary` + atomic `TryAdd`/`TryRemove` |
| High | `EngineeringExecutionPipeline.TryGetRepositoryContext` | Guard checked `is string`, not `IsNullOrEmpty` — an empty (not missing) `RepositoryRoot`/`RepositoryName` payload value threw an unhandled `ArgumentException` from deeper adapters instead of the method's own documented graceful clarification diagnostic | Added `!string.IsNullOrEmpty(...)` to both checks |
| Medium | `LocalGitOperations.GetStatus` | `git status --porcelain` rename/copy lines (`"R  old -> new"`) were parsed as a flat `XY path` line, surfacing the literal arrow-separated string as a "changed path" | Detect `R`/`C` markers, split on `" -> "`, keep the destination path |

**Audited with no confirmed Critical/High/Medium defect:** `LocalFileSystemRepositoryOperations`/`LocalFileSystemRepositoryScanner` (root-boundary/symlink-escape logic traced end-to-end, no bypass found), `Program.cs`/`RuntimeStartupHostedService`/`PermissiveActivationPolicyResolver` (startup validation correctly wired), `GovernanceBoundary` (stateless per call).

Landed via `feature/p4-runtime-hardening` → PR #25 → squash-merged to `main`, branch deleted. Full suite verified green 3 consecutive runs post-fix (324/324 — the 7 new regression tests bring the total from 317 to 324).

**Known, pre-existing, out-of-scope-for-P4 flake:** the Windows temp-git-repo test cleanup pattern (`DeleteRepository` helpers, duplicated per-test-file by this codebase's own established convention) can intermittently hit a transient `IOException` ("file in use by another process") when many git-based tests run in parallel — observed once each on two different, unrelated pre-existing test files during this session, disappeared on immediate re-run. Hardened the one helper this session's own new test touches (`LocalGitOperationsTests.DeleteRepository`, retry-with-backoff); did not touch every other test file's independent copy of the same helper, since that is test-infra reliability, not a production defect, and out of proportion to P4's scope.

## Phase 4 — Performance Baseline

Measured on this machine, 2026-07-08 (see the RC0 package's Runtime Metrics section for the full numbers): Host cold boot to `Application started` ~1.0–1.1s (registry load itself ~225ms of that); a real Repository Intelligence request against Ferret 533–711ms; idle Host working set ~41.5 MB; full 324-test suite ~30s combined. No performance concern found — nothing warranted speculative optimization.

## Phase 5 — Final Validation

Demonstrated via the combination of (a) the existing, passing `Host.Tests` integration suite (`EngineeringExecutionPipelineHostIntegrationTests`, `RepositoryEngineeringHostIntegrationTests` — Plan and Validate dispatch paths, against the real booted Host) and (b) this session's real Ferret dogfooding run (Analyze/Diagnostics dispatch paths, against a genuinely external repository): Runtime starts ✓, Repository loads ✓, Engineering request executes ✓, Repository analysed ✓, Planning executed ✓ (existing test), Skills executed ✓, Contracts executed ✓ (Prompt Contract shapes honored throughout), Validation completed ✓ (existing test), Structured `ExecutionResult` returned ✓, Diagnostics recorded ✓.

## Phase 6 — RC0

See the RC0 package (Release Notes, Capability Matrix, Validation Report, Known Limitations, Test Summary, Runtime Metrics, Dogfooding Summary) and the `AEF-v1.0-RC0` tag.

## Explicitly out of scope for P4 (per its own Mandatory Rules)

- `AGT-EXE-0013` (Specification Reviewer) / `AGT-EXE-0014` (Validation Agent) remain unimplemented — unchanged, documented gap from P2/P3. Both require genuine NL/LLM-backed reasoning with zero deterministic-algorithm precedent and no LLM-invocation abstraction anywhere in this Runtime; implementing shallowly would produce a hollow registration. This is a new-capability decision, not a hardening task.
- No external interface (HTTP/CLI) exists for submitting a live `EngineeringRequest` to a running Host process — the Host currently only boots and registers; every proof of the pipeline (existing tests and this session's dogfooding) invokes `EngineeringExecutionPipeline` in-process. Adding one would be new operational surface, not a defect fix; recorded as a Known Limitation rather than built speculatively.
