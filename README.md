# AEF Platform Repository

The Platform Repository, per `epics/decisions/0002-platform-repository-and-technology-selection.md` in the [AI Engineering Framework](../ai-engineering-framework) repository (AD-71: exactly one Platform Repository, structurally separate from AEF's own governance/knowledge/prompt content).

Hosts the Runtime Kernel and, eventually, the fifteen platform Construction Units `docs/aep/I4-Platform-Construction-Implementation-Planning.md` defines. This repository contains no product-specific or consuming-repository content — only Kernel/Service/Host implementation code.

## Status

**Epic 0004 (Epic-Granularity Autonomous Lifecycle, CU-2) in progress: Stories S16-S17 done, S18 not yet started.** `SKL-EXE-0012`/`AGT-EXE-0005` (Next-Eligible-Item Discovery) and `SKL-EXE-0013`/`SKL-EXE-0014`/`AGT-EXE-0006`/`WFL-DEL-0004` (Epic-Granularity Lifecycle Adaptation) are Registered and Active, implemented as `src/Runtime/DomainContent/EpicLifecycle/` and `src/Runtime/Orchestration/EpicLifecycleOrchestrator.cs`. See `epics/0004-epic-granularity-autonomous-lifecycle.md` in the AEF repository for the full Story decomposition and Definition of Done.

**Epic 0001 (Runtime Kernel Construction) complete: all four Stories (S1-S4) done.** The Kernel exposes three public contracts (I4 §8) — a Registration Contract, an Activation Contract, and a Discovery read-contract — all implemented, composed, and bootable through a single `KernelBootstrapper.Boot()` entry point (`src/Kernel/Bootstrapping/`). See `epics/0001-runtime-kernel-construction.md` in the AEF repository for the full Story decomposition and Definition of Done.

**Epic 0002 (Walking Skeleton) complete: all six Stories (S5-S10) done.** The Execution Context Manager, the three Hosts (Agent/Skill/Strategy), and the Runtime Catalog are implemented, composed, and bootable through `RuntimeBootstrapper.Boot()` (`src/Runtime/Bootstrapping/`), extending `KernelBootstrapper.Boot()` rather than replacing it. See `epics/0002-walking-skeleton.md` in the AEF repository for the full Story decomposition and Definition of Done.

**Epic 0003 (Feature Lifecycle Dogfooding, CU-2) complete: all five Stories (S11-S15) done.** D9's frozen, `Active` Feature-lifecycle content (`SKL-EXE-0004`, `SKL-EXE-0005`, `AGT-EXE-0002`, `WFL-DEL-0001`, `ORC-EXE-0001`) and D10's frozen orchestration design (Workflow Instance state machine, Manual trigger, Human-Approval-Gates routing) now run end-to-end on this Runtime for the first time — the first real, non-synthetic governed content ever executed here. See `epics/0003-feature-lifecycle-dogfooding.md` in the AEF repository for the full Story decomposition and Definition of Done.

## Layout

### `Kernel` (Epic 0001, Construction Units 1-3)

- `src/Kernel/Registry/` — the Registry write-side (S1): definition submission, entry, register/query results, the `IRegistry` contract, and `InMemoryRegistry`.
- `src/Kernel/Governance/` — the Governance Boundary enforcement point (S2): the Activation Contract's gate, checking Registered/`Active` status via `IRegistry`, then a pluggable `IPolicyResolver` extension point (fail-closed by default).
- `src/Kernel/Supervision/` — the Instance Supervisor (S3): creates/destroys Runtime Instances for Activation requests that have cleared S2, via a pluggable `IResourceAdmissionGate` (AD-72; unconstrained by default).
- `src/Kernel/Bootstrapping/` — Kernel Bootstrapping (S4): `KernelBootstrapper.Boot()` composes S1→S2→S3 in dependency order, sharing one Evidence sink, returning a `KernelHandle` exposing all three contracts.
- `src/Kernel/Evidence/` — the pluggable Evidence-sink extension point (`epics/decisions/0001-kernel-evidence-emission-before-store-exists.md`), invoked by every Kernel accept/reject decision ahead of the real Knowledge & Evidence Store (Construction Unit 5, Milestone M2).
- `tests/Kernel.Tests/Registry/`, `tests/Kernel.Tests/Governance/`, `tests/Kernel.Tests/Supervision/`, `tests/Kernel.Tests/Bootstrapping/` — implementation-specific unit tests.
- `tests/Kernel.Tests/Contracts/` — the storage/policy-independent contract test suites, written against public interfaces only.
- `tests/Kernel.Tests/Scenarios/` — end-to-end dogfooding scenarios, including the full Register → Activate → confirm Running → Destroy → Reboot → confirm clean state walking skeleton (Epic 0001's Definition of Done #3).

### `Runtime` (Epic 0002, Construction Units 4, 12-15)

- `src/Runtime/Context/` — the Execution Context Manager (S5, Unit 4): `RuntimeExecutionContext` (the four-tier record — named to avoid colliding with `System.Threading.ExecutionContext`), `ContextSeed`, the pluggable `IContextDisposalPolicy` (default: unconditional discard), and `ExecutionContextManager`.
- `src/Runtime/Hosts/Agent/` — the Agent Host (S6, Unit 12): `AgentHost` + `AgentInstanceHandle`, exposing exactly `InvokeSkill`/`SignalGap`; delegates Skill invocation to a pluggable `ISkillInvoker` (the Skill Host implements it).
- `src/Runtime/Hosts/Skill/` — the Skill Host (S7, Unit 13): `SkillHost` (implements `ISkillInvoker`), executing a registered Skill's bound `IBehavior` synchronously against the caller's Context; never touches `IInstanceSupervisor`.
- `src/Runtime/Hosts/Strategy/` — the Strategy Host (S8, Unit 14, widened by S11): `StrategyHost` + `StrategyInstanceHandle`, exposing exactly `AllocateStep`/`PauseForApproval`/`PauseForClarification`/`PauseForTerminalFailure`/`Resume`/`Complete`/`Terminate`; the Workflow Instance state machine's closed-set transition table (`docs/discovery/orchestration/01-orchestrator-design.md` §2) lives in `SessionTransitions`; allocation delegates to a pluggable `IAgentAllocator` (the Agent Host is adapted to it at Bootstrapping).
- `src/Runtime/Catalog/` — the Runtime Catalog (S9, Unit 15): `RuntimeCatalog`, a stateless, live-delegating read projection of `IRegistry`, with an optional `IContractConformanceProvider` extension point.
- `src/Runtime/Bootstrapping/` — Runtime Bootstrapping (S10): `RuntimeBootstrapper.Boot()` extends `KernelBootstrapper.Boot()` through I3 §10 steps 3-5 (initialize the Execution Context Manager, wire the three Hosts and the Catalog, reach Runtime Ready), returning a `RuntimeHandle`.
- `src/Runtime/Orchestration/` — the Orchestrator (S13-S14): `FeatureLifecycleOrchestrator` realizes `ORC-EXE-0001` operating `WFL-DEL-0001` specifically (D10 §1.2's "select and start a Workflow instance" / "allocate an Agent to a step" responsibilities) — `Start(ManualTrigger)` begins one Workflow Instance and allocates its first step; `SupplyClarification`/`ResolveRoadmapReview` resolve Human-Approval-Gates-table entries #2/#3 (`docs/discovery/orchestration/09-human-approval-gates.md`), the latter requiring a typed `ReviewDecision`, never a plain string.
- `src/Runtime/DomainContent/FeatureLifecycle/` — the executable realization of `SKL-EXE-0004`/`SKL-EXE-0005`'s own Behavior sections (S15): `FeatureWorkspace` (an in-memory stand-in for one `features/<name>.md` instance's accumulating sections — no persistence yet), `IntakeAndClarificationBehavior`, `RoadmapAndPlanningBehavior`.
- `tests/Runtime.Tests/Context/`, `tests/Runtime.Tests/Hosts/Agent/`, `tests/Runtime.Tests/Hosts/Skill/`, `tests/Runtime.Tests/Hosts/Strategy/`, `tests/Runtime.Tests/Catalog/`, `tests/Runtime.Tests/Orchestration/` — implementation-specific unit tests.
- `tests/Runtime.Tests/Scenarios/` — end-to-end dogfooding scenarios: the Epic 0002 walking skeleton (synthetic `walking-skeleton-echo-*` content, per `epics/decisions/0004-...md`); the Epic 0003 Feature Lifecycle dogfooding scenario — the first real, non-synthetic D9 content ever Registered and Activated on this Runtime, and the Registration-only proof (`D9FeatureLifecycleContentRegistrationTests`) that precedes it.

### `Epic 0004` (Epic-Granularity Autonomous Lifecycle, CU-2 second Epic)

- `src/Runtime/DomainContent/EpicLifecycle/` — the executable realization of `SKL-EXE-0012`'s Behavior (S16): `CandidateItem` (one candidate-table row), `DiscoveryOutcome` (`Eligible`/`NoneEligible`/`Ambiguous`), `NextEligibleItemDiscoveryBehavior` (queries the unmodified Runtime Catalog per candidate; never mutates the candidate table it is handed). Does not touch `DomainContent/FeatureLifecycle/` or `AGT-EXE-0002` — a new, Epic-scoped folder, per the Epic's own explicit non-goal.
- `tests/Runtime.Tests/DomainContent/EpicLifecycle/` — `NextEligibleItemDiscoveryBehaviorTests` (4 tests, S16); `EpicIntakeAndClarificationBehaviorTests`, `EpicRoadmapAndPlanningBehaviorTests` (S17: `EpicWorkspace`, `EpicIntakeAndClarificationBehavior`, `EpicRoadmapAndPlanningBehavior` — the Epic-scoped, independent siblings of `FeatureLifecycle`'s equivalents; no dependency between the two namespaces).
- `tests/Runtime.Tests/Scenarios/NextEligibleItemDiscoveryRegistrationTests.cs` — registers `SKL-EXE-0012`/`AGT-EXE-0005` and proves the Skill invocable through the Skill Host's public contract (S16, 2 tests).
- `tests/Runtime.Tests/Scenarios/EpicLifecycleContentRegistrationTests.cs` — registers `SKL-EXE-0013`/`0014`/`AGT-EXE-0006`/`WFL-DEL-0004` (S17, 1 test).
- `src/Runtime/Orchestration/EpicLifecycleOrchestrator.cs` (S17) — the Epic-scoped sibling of `FeatureLifecycleOrchestrator`, bound to `WFL-DEL-0004`/`AGT-EXE-0006`; reuses `StrategyHost`/`SessionTransitions`/`ReviewDecision`/`GateResumeResult`/`OrchestrationStartResult` verbatim. `tests/Runtime.Tests/Orchestration/EpicLifecycleOrchestratorTests.cs`, `EpicHumanApprovalGateRoutingTests.cs` (5 tests).
