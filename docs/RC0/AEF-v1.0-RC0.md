# AEF v1.0 — Release Candidate 0 (RC0)

**Tag:** `AEF-v1.0-RC0` · **Date:** 2026-07-08 · **Repository:** `aef-platform` (this repository — the Platform Repository per `epics/decisions/0002-...md` in the `ai-engineering-framework` repository)

## Release Notes

RC0 closes the "AEF v1.0 RC0" sequence (P1 Runtime Foundation → P2 Engineering Execution Engine → P3 Repository Engineering Engine → P4 Runtime Hardening, Dogfooding & RC0). The Runtime boots, registers its full governed architecture, and executes real engineering requests — Feature/Epic lifecycle orchestration, Delivery Execution, Conversation-Manager-routed dispatch, Engineering Planning, and Repository Intelligence/Validation against a real, external repository — returning a structured, honestly-reported result. P4 specifically: ran a real (non-simulated) dogfooding session against the Ferret repository, commissioned an independent code-quality audit of the runtime's process-invocation/Kernel-state/pipeline-dispatch surfaces, and fixed every Critical/High defect found (PR #25) with regression tests. No architecture change, no new Capability Unit, no new agent was introduced — P4 stayed entirely inside the already-frozen, already-Active architecture.

## Capability Matrix

36/36 governed artifacts registered at every boot (`Runtime Architecture Loader: 36/36 artifacts registered.`, fail-fast on any registration failure):

| Capability | Agent(s) | Skill(s) | Workflow |
|---|---|---|---|
| Orchestration (Feature/Epic lifecycle state machine) | — | — | `ORC-EXE-0001` |
| Feature Lifecycle | `AGT-EXE-0002` | `SKL-EXE-0004`, `SKL-EXE-0005` | `WFL-DEL-0001` |
| Next-Eligible-Item Discovery | `AGT-EXE-0005` | `SKL-EXE-0012` | — |
| Epic-Granularity Lifecycle | `AGT-EXE-0006` | `SKL-EXE-0013`, `SKL-EXE-0014` | `WFL-DEL-0004` |
| Repository Assessment & Profile Selection (onboarding) | `AGT-EXE-0007` | `SKL-EXE-0015` | — |
| Registration & Workspace Initialization | `AGT-EXE-0008` | `SKL-EXE-0016` | — |
| Baseline Quality Gate & Exception Recording | `AGT-EXE-0009` | `SKL-EXE-0017` | — |
| Delivery Execution (Implementation/Validation/Documentation) | `AGT-EXE-0010` | `SKL-EXE-0018/0019/0020` | — |
| Conversation Manager (intake/routing, handoff/followup) | `AGT-EXE-0011` | `SKL-EXE-0021/0022` | — |
| Engineering Planner (Discovery, Spec Drafting, Readiness) | `AGT-EXE-0012` | `SKL-EXE-0023/0024/0025` | `WFL-DEL-0006` |
| Repository Intelligence (Understanding, Code Intelligence, Diagnostics, Synthesis) | `AGT-EXE-0015` | `SKL-EXE-0030/0031/0032/0033` | `WFL-DEL-0008` |
| Repository Validation (build/test detection + execution) | `AGT-EXE-0010` | `REPO-VALIDATION` | — |

**Documented, unimplemented (unchanged since P2/P3 — see Known Limitations):** `AGT-EXE-0013` (Specification Reviewer), `AGT-EXE-0014` (Validation Agent).

## Validation Report

| Check | Result |
|---|---|
| `dotnet build AefPlatform.sln` | Build succeeded, 0 warnings, 0 errors |
| `dotnet test AefPlatform.sln` | 324/324 passing (48 Kernel.Tests, 21 Host.Tests, 255 Runtime.Tests) — verified green on 3 consecutive runs post-fix |
| Host boot (`dotnet run`, real process) | Starts, registers 36/36 artifacts, serves, shuts down gracefully on stop signal |
| Real dogfooding (Ferret, external repo) | 3/3 real Repository Intelligence requests succeeded with honest, evidence-grounded findings; 1/1 negative-routing case correctly requested clarification instead of guessing |
| Independent code-quality audit | 5 real defects found (1 Critical class affecting 2 files, 2 High, 1 Medium), all fixed with regression tests; 3 areas audited with no confirmed defect above Medium |
| Final validation demonstration | Runtime starts ✓ · Repository loads ✓ · Engineering request executes ✓ · Repository analysed ✓ · Planning executed ✓ · Skills executed ✓ · Contracts executed ✓ · Validation completed ✓ · Structured result returned ✓ · Diagnostics recorded ✓ |

## Test Summary

- **Kernel.Tests:** 48 passing (Registry, Governance, Supervision, Bootstrapping, Contracts, Scenarios) — includes 2 new concurrency regression tests (`InMemoryRegistry` duplicate-race and many-distinct-Ids-concurrently; `InstanceSupervisor` many-concurrent-`CreateInstance`).
- **Host.Tests:** 21 passing (startup validation, policy resolver, architecture loader, pipeline↔Host integration for Analyze/Plan/Validate dispatch).
- **Runtime.Tests:** 255 passing (Context, Hosts, Catalog, Orchestration, DomainContent across every Epic 0001–0008 + P1–P3 capability, Scenarios/dogfooding) — includes 5 new regression tests (`ProcessVerificationRunner` large dual-stream deadlock guard; `LocalGitOperations` rename-parsing + large-diff deadlock guard; `EngineeringExecutionPipeline` empty-payload clarification).
- **Total: 324/324, 0 skipped, 0 warnings.**

## Runtime Metrics (measured 2026-07-08, this machine — indicative, not a formal SLA)

| Metric | Value |
|---|---|
| Host cold boot → `Application started` | ~1.0–1.1s (registry load itself ~225ms of that) |
| Repository Intelligence request against a real external repo (Ferret) | 533–711ms per request (filesystem scan + git status + stack detection + 3–4 chained skill invocations) |
| Idle Host process working set | ~41.5 MB |
| Full 324-test suite | ~30s combined (Kernel ~30ms, Host ~7–13s, Runtime ~17–25s) |

No performance concern surfaced; no speculative optimization applied per P4's own Mandatory Rules.

## Dogfooding Summary

Target: `C:\POC\Ferret` (a real, independently-maintained repository, `origin` = `https://github.com/indoulia/Ferret.git`, clean working tree on `main` at the time of the run). Harness: the real, booted `RuntimeHandle` (36/36 artifacts) + `EngineeringExecutionPipeline.Execute()`, invoked via a temporary (never committed) xUnit test reusing `RepositoryEngineeringHostIntegrationTests`'s own established pattern.

1. **Broad natural-language request** ("Analyze this repository's architecture and code quality signals.") — `Success=true`, dispatched to `WFL-DEL-0008`/`AGT-EXE-0015`. Correctly reported `[Unknown]` for the literal-sentence structural lookup (no NL/LLM capability exists — honest, not fabricated), while a real evidence-derived `[Pass]` finding confirmed a conventional test directory exists at Ferret's actual repository root.
2. **Scoped request** (`RequestScope="docs"`) — found and reported Ferret's real `docs/` directory.
3. **Diagnostics request** (`ReportedProblem="CI pipeline runs are slow"`, `RequestScope="build"`) — found Ferret's real `build/` directory; root-cause confidence fixed at a low, honestly-justified `2/10` with one alternative hypothesis per evidence source, never a fabricated causal chain.
4. **Negative routing case** (a phrasing with no `SKL-EXE-0021` keyword) — correctly declined with a clarification request rather than guessing.

`REPO-VALIDATION` was deliberately not run against Ferret's real files (would trigger Ferret's actual, unbounded build/test commands) — that dispatch path is proven via the existing `Host.Tests` suite against a fast, disposable synthetic repository instead.

**No defects were found from the dogfooding exercise itself** — the audit-sourced defects (see Validation Report) came from an independent code review of the underlying process/state-management code, not from anything Ferret's real files exposed.

## Known Limitations

1. **`AGT-EXE-0013` (Specification Reviewer) and `AGT-EXE-0014` (Validation Agent) are unimplemented.** Both require genuine NL/LLM-backed reasoning with zero deterministic-algorithm precedent anywhere in this Runtime, and no LLM-invocation abstraction exists yet. Implementing either shallowly would produce a hollow, fake registration — documented and deferred since P2, unchanged by P3 or P4. This is a new-capability decision for a future, separately-authorized prompt, not a hardening gap.
2. **No external interface (HTTP/CLI) exists for submitting a live `EngineeringRequest`** to a running Host process. The Host currently boots, registers its architecture, and idles; every proof of the pipeline (existing tests, and this release's own dogfooding evidence) invokes `EngineeringExecutionPipeline` in-process. Adding an operational entry point is new surface, not a defect fix — recorded here rather than built speculatively.
3. **Deterministic-only NL interpretation throughout.** Every Skill specified as free-text/NL reasoning (routing, repository understanding, code intelligence, root-cause diagnostics, specification drafting) is realized as a deterministic, evidence-grounded structural check that honestly reports `UNKNOWN`/a fixed low confidence/a clarification request wherever real judgment is required — by design, per every Prompt since P2, never a fabricated answer. This is the correct, non-hollow realization of these Contracts, not a shortfall to silently improve.
4. **A pre-existing, intermittent Windows test-cleanup flake** (`IOException: file in use by another process`, transient, on temp-git-repo test teardown under parallel test execution) was observed twice during this session on two different, unrelated pre-existing test files; both passed on immediate re-run. Test-infra reliability, not a production defect — not fixed suite-wide (only the one helper this release's own new test touches was hardened), since a suite-wide fix is out of proportion to P4's scope.
5. **Runtime metrics above are indicative, not benchmarked.** Single-machine, single-run-set measurements — no formal performance SLA is established by this release.

## Recommendation for GA Readiness

RC0's own scope (prove P1–P3 stable via real dogfooding + hardening) is met: 324/324 tests green, 0 Critical/High defects remaining, a real external-repository dogfooding session completed with zero fabricated output. The two Known Limitations that most affect GA readiness are #1 (Specification Reviewer/Validation Agent gap — blocks a fully autonomous Engineering Delivery Core Loop) and #2 (no external interface — blocks any real, non-test consumer from using this Runtime today). Neither is a defect; both are genuine, Founder-scoped next-capability decisions outside P4's delegated hardening authority. Recommend: Founder review of whether to authorize (a) an LLM-invocation abstraction (unlocking Specification Reviewer/Validation Agent for real), and (b) an external interface Capability Unit, as the next Phase — GA should not be declared before at least one real, non-test consumer has exercised this Runtime end-to-end through whatever interface is authorized.
