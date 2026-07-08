# GA-3: External Execution Channels — Architecture & Engineering Program

**Status:** Architecture approved (Founder, 2026-07-08). This document is the authoritative roadmap for GA-3. It is architecture and engineering planning only — no Engineering Specifications, no implementation, no code. Per-channel design specs (the GA-2-style `-design.md` documents) and their implementation plans are produced in later GA-3 prompts, one per Capability below, and must cite this document rather than re-deriving its decisions.

## Context

GA-1 (`Runtime.Ai` — provider-neutral AI integration) and GA-2 (`Runtime.Agent` — the product-facing execution interface: `ExecutionRequest`/`ExecutionResponse` contracts, capability discovery, session model, the router) are complete and merged. `ADR-0025` (`ai-engineering-framework/epics/decisions/0025-public-execution-interface-boundary.md`) binds every future external caller to enter governed execution exclusively through `Runtime.Agent`, never through `EngineeringExecutionPipeline` directly.

Today, `Runtime.Agent` is a library with no live entry point — `Host`'s `Program.cs` boots dependency injection, validates the capability registry, then calls `RunAsync()` and idles forever. Every existing exercise of the pipeline (324/324 tests, RC0's Ferret dogfooding run) has been a single caller, one request at a time, in-process. GA-3's job is to give `Runtime.Agent` its first live callers.

## 1. GA-3 Vision

GA-3's purpose is to transform `aef-platform` from an internally-exercised execution library into a reachable platform, without weakening the execution boundary GA-2 and ADR-0025 established. It does this by shipping exactly two channels — CLI and REST — and by declaring REST the durable, versioned integration surface every other future channel builds on, rather than treating each future channel as its own execution path.

**Success criteria:**

- Every request, on every channel, resolves to the same canonical `ExecutionRequest` and is executed by calling `IAgentExecutionInterface` — no channel contains routing, dispatch, or capability-selection logic of its own.
- The dependency direction is a compile-time/test-enforced guarantee: CLI and REST reference `Runtime.Agent` only, never `Runtime`, `Runtime.Ai`, or `Kernel`.
- A generic, long-running **AEF Runtime Host** exists and hosts REST as its first adapter — the host is a platform capability, not "the REST project."
- A real, Ferret/Garuda-shaped engineering request executes successfully end-to-end through both CLI and REST against the existing 36/36-artifact runtime, with the full 324+ test suite still green.
- Security is an explicit, demonstrated seam (request → security abstraction → `Runtime.Agent`) with a working trusted-default implementation — not a documented intention.
- Concurrency safety under real, overlapping HTTP requests is verified, not assumed — this is GA-3's one genuinely new technical risk, since nothing in this runtime has ever been exercised by more than one caller at a time.

## 2. Execution Channels

| Channel | GA-3 Disposition | Rationale |
|---|---|---|
| **CLI** | **In scope** | Native human/scripting interface; direct, in-process caller of `Runtime.Agent`; short-lived process, zero new hosting model needed. |
| **REST API** | **In scope** | The canonical remote/integration surface. Versioned from day one because every future channel below is chartered to build on it. |
| SDK | Deferred — future consumer of REST | A typed client library over the REST contract, not an independent execution path. No value shipping it before the contract it wraps is stable. |
| IDE Integration | Deferred — future consumer of REST | An editor extension calling REST; no in-process or protocol-specific execution logic of its own. |
| Chat Interface | Deferred — future consumer of REST | A conversational front end calling REST; `Runtime.Agent`'s session model already carries multi-turn state, so a chat surface needs no new execution concept. |
| Automation | Deferred — future consumer of REST | CI/CD and scheduled callers invoking REST (or CLI, inside a pipeline step) with no bespoke integration surface. |
| Batch Execution | Deferred — future consumer of REST | Modeled as a calling pattern (looped/queued REST calls), not a distinct endpoint or execution path, unless a later GA identifies a genuine need for server-side batching. |

None of the five deferred channels are re-evaluated as "maybe never" — they are explicitly named future roadmap items, scoped out only because they have no execution model to invent once REST exists.

## 3. Channel Architecture

```
External Client (CLI invocation, or HTTP caller of REST)
        ↓
CLI  or  REST adapter          (transport parsing/serialization + ExecutionRequest/Response translation only)
        ↓
Runtime.Agent                  (IAgentExecutionInterface — the sole execution boundary, per ADR-0025)
        ↓
EngineeringExecutionPipeline   (internal — never referenced directly by a channel)
```

- **CLI**: a separate, short-lived executable. Parses invocation arguments, constructs an `ExecutionRequest`, calls `Runtime.Agent` in-process, prints the `ExecutionResponse`, exits. Carries no server, no listener, no shared state across invocations beyond what `Runtime.Agent`'s own session model already provides (a session id round-tripped by the caller, exactly as GA-2 designed it).
- **REST API**: a thin HTTP adapter — deserializes a request body into `ExecutionRequest`, calls `Runtime.Agent`, serializes the `ExecutionResponse` back out. Contains transport concerns only (HTTP status mapping, content negotiation, versioned routing) — no capability-selection or routing logic, per ADR-0025 Decision 3.
- **AEF Runtime Host**: `Host`'s idle placeholder becomes a generic, long-running composition root that hosts one or more adapters. REST is its first hosted adapter. The host itself owns nothing channel-specific — it assembles the same DI graph GA-1/GA-2 already built (`IAiProvider`, `AiExecutionService`, behaviors, `AgentExecutionService`, `ISessionStore`, `ICapabilityCatalog`) once, and adapters attach to it. Future adapters (health endpoints, SignalR, gRPC, webhooks) are named as illustrative extension points for later GAs — none are built in GA-3.
- **Both channels depend on `Runtime.Agent` only.** This is verified by an architecture fitness test (a dependency-direction check over project references, or an equivalent static check), not left to review discipline alone — the same enforcement principle ADR-0025 already established for the `Runtime.Agent`/`EngineeringExecutionPipeline` boundary, extended one layer out to cover the two new channel projects.

**Explicit Non-Goals for GA-3** (mirroring the discipline GA-2 itself used): no SDK, IDE, Chat, Automation, or Batch implementation. No multi-tenant identity provider integration. No new hosted adapter beyond REST (health/SignalR/gRPC/webhooks are named, not built). No redesign of `Runtime.Ai`, `Runtime.Agent`'s contracts, or `EngineeringExecutionPipeline` — GA-3 is a channel-and-host program, not a capability-redesign program.

## 4. Engineering Program

Five capabilities, sequenced. Each becomes its own design spec + implementation plan in a later GA-3 prompt; this section defines scope and ordering only.

### Capability 1 — AEF Runtime Host

- **Epics:** Generic adapter-hosting model; security abstraction seam (with trusted-default implementation); concurrency-safety audit of the existing DI graph (`AiExecutionService`, behaviors, `InMemorySessionStore`, and any other singleton) under overlapping callers.
- **Features:** Host composition root replacing today's idle `Program.cs`; adapter registration mechanism; `ISecurityContext`-shaped seam every adapter passes a request through before it reaches `Runtime.Agent`; a documented, tested concurrency model for every shared service.
- **Sequence position:** First. Nothing hosted can be built before the host exists, and the concurrency audit must land before REST accepts overlapping requests for real.
- **Completion Gate:** Host boots, hosts zero adapters cleanly (smoke-testable in isolation), the security seam has a trusted-default implementation with a passing test, and the concurrency audit has a written finding (safe as-is, or the specific fix it requires) for every shared singleton.

### Capability 2 — REST API Channel

- **Epics:** Versioned HTTP surface (`/v1`) over `ExecutionRequest`/`ExecutionResponse`; capability discovery endpoint over `ICapabilityCatalog`; session-carrying endpoint semantics (session id in, session id out, matching GA-2's existing session contract); error-shape mapping (`ExecutionStatus`/`ExecutionError` → HTTP status + problem-details body); cancellation propagation from client disconnect into the existing `CancellationToken` parameter.
- **Features:** `POST /v1/execute`, `GET /v1/capabilities`, `POST /v1/sessions` (or equivalent — exact routes are a design-spec decision, not fixed here); OpenAPI/contract documentation generation.
- **Sequence position:** Second, hosted inside Capability 1's host once it exists.
- **Completion Gate:** A real request executes end-to-end over HTTP against the live 36/36-artifact runtime; every `ExecutionStatus` value maps to a distinct, documented HTTP outcome; concurrent requests (per Capability 1's audit) behave correctly under a real concurrent-load test, not just sequential tests.

### Capability 3 — CLI Channel

- **Epics:** Argument parsing → `ExecutionRequest` construction; response formatting; session id persistence across invocations (a local convenience, e.g. an environment variable or a flag — a design-spec decision) so a human can hold a multi-turn conversation across separate process invocations.
- **Features:** `execute` command (single request/response); `capabilities` command (lists `ICapabilityCatalog.Discover()`); exit-code convention mapping `ExecutionStatus` to process exit codes for scriptability.
- **Sequence position:** Third — deliberately after REST, since CLI is architecturally simpler (no host, no concurrency) and de-risking the shared `Runtime.Agent` contracts through REST first benefits CLI for free.
- **Completion Gate:** A real request executes end-to-end via the CLI against the live runtime; scripted/automated invocation (exit codes, machine-parseable output mode) is demonstrated, since Automation is a named future consumer of exactly this scriptability.

### Capability 4 — Cross-Cutting Hardening

- **Epics:** Operational logging (distinct from `IEvidenceSink`'s governance-evidence recording — this is standard application/request logging, which does not exist anywhere in the runtime today); health/diagnostics reporting; graceful shutdown (drain in-flight requests before process exit); observability (request-level metrics: count, latency, error rate).
- **Features:** Structured log output for host lifecycle and per-request outcome; a liveness/readiness signal (used by Capability 1's host, exposed by Capability 2's REST channel — this is the one piece of Capability 2 that is genuinely cross-cutting rather than REST-specific); shutdown draining tied to the host's lifecycle hooks.
- **Sequence position:** Fourth — applied across both channels once both exist, rather than duplicated per-channel during Capabilities 2/3.
- **Completion Gate:** A running host reports health honestly (fails readiness if a dependency, e.g. the configured AI provider, is unreachable); a shutdown signal drains in-flight requests instead of dropping them; logs are sufficient to reconstruct what a given request did without consulting `IEvidenceSink`.

### Capability 5 — Documentation & Release Readiness

- **Epics:** All documentation named in Section 7; performance validation named in Section 8; final regression run.
- **Features:** N/A at program level — this capability is the release gate itself, detailed in Section 9.
- **Sequence position:** Last, gating the release.
- **Completion Gate:** Section 9 in full.

## 5. Cross-Cutting Capabilities

| Capability | GA-3 Disposition |
|---|---|
| Authentication | **Seam, not a gate.** A security abstraction every request passes through before reaching `Runtime.Agent`, with a trusted-default implementation (Capability 1). Real identity-provider integration is a deployment decision for a later GA, not a GA-3 blocker. |
| Authorization | **Seam, not a gate.** Same abstraction as Authentication; a trusted-default implementation grants full access. Capability-level access control (e.g. per-caller allow-lists) is an extension point, not required to ship. |
| Configuration | **In scope.** Extends the existing `IOptions<T>`-bound convention (`PlatformHostOptions`, `AiProviderOptions`) to host/adapter configuration (e.g. listen address, timeouts). No new configuration paradigm. |
| Logging | **In scope, new.** Operational/request logging does not exist today (only governance evidence via `IEvidenceSink`) — see Capability 4. |
| Diagnostics | **In scope.** Extends `ExecutionResponse.Evidence.Diagnostics`, already designed in GA-2, plus host-level health/diagnostics from Capability 4. |
| Session Management | **In scope, wiring only.** `ISessionStore`/`ExecutionSession` already exist from GA-2 — GA-3's job is to have REST actually carry a session id per the existing contract, not redesign the session model. |
| Progress Reporting | **Partially in scope.** `IExecutionProgressObserver` already exists from GA-2. GA-3 wires it where a channel can meaningfully surface it (e.g. CLI could print progress); a true streaming transport (SSE/WebSockets) over REST is explicitly deferred — GA-3 REST is request/response, not streaming. |
| Cancellation | **In scope.** An HTTP client disconnect must propagate into the same `CancellationToken` the pipeline already accepts — no new cancellation concept, just a real caller of the existing one. |
| Resilience | **In scope, reused.** `RetryExecutor`/`AiExecutionService` (GA-1) already handle AI-call resilience. GA-3 adds process-level resilience for the host itself (graceful shutdown, request timeout policy) — Capability 1/4. |
| Error Handling | **In scope.** Consistent `ExecutionStatus`/`ExecutionError` → HTTP status mapping (Capability 2); CLI exit-code mapping (Capability 3). |
| Observability | **In scope.** Health signal + basic request metrics (Capability 4). Full metrics/tracing infrastructure (e.g. a specific APM vendor) is a deployment decision, not a GA-3 architectural requirement. |
| Versioning | **In scope, hard requirement.** REST is versioned (`/v1`) from its first release because it is chartered as the durable integration surface every future channel builds on — an unversioned "canonical" surface is a contradiction. |

## 6. Product Execution

Ferret, Garuda, and future Engineering Programs orchestrate entirely as callers of the existing contracts — no product-specific code exists anywhere in `Runtime.Agent`, the channels, or the host. A program's orchestration shape is:

1. **Discover** — call `GET /v1/capabilities` (or the CLI `capabilities` command) to learn what the runtime can execute, rather than hardcoding capability ids.
2. **Start or resume a session** — a program that expects multi-turn interaction (e.g. a specification review cycle) obtains a session id and reuses it; a one-shot program omits it, exactly as GA-2 designed.
3. **Execute** — submit a `ProjectProfileId`-scoped `ExecutionRequest` naming the program's own repository/profile context; the runtime performs all routing/dispatch internally.
4. **Handle the outcome** — `Completed` (consume `PresentedResult`/evidence), `NeedsClarification` (the program decides whether to re-prompt a human or resolve programmatically), or `Failed` (the program's own retry/escalation policy, entirely outside this runtime).

This is orchestration description only — no product (Ferret's, Garuda's, or a future program's) gets bespoke execution logic inside `aef-platform`. A new Engineering Program is onboarded by pointing it at the same REST contract with its own `ProjectProfileId`, nothing more.

## 7. Documentation

Required for GA:

- **Architecture** — this document, plus per-channel design specs (Capabilities 1–3) once written, following the GA-2 `-design.md` precedent.
- **Developer Guide** — how to call `IAgentExecutionInterface` in-process (already a GA-2 deliverable) plus how to run the host locally.
- **CLI Guide** — commands, arguments, exit codes, session-id convention.
- **REST Guide** — endpoint reference (ideally OpenAPI-generated), authentication seam (trusted-default vs. future real auth), versioning policy.
- **Operations Guide** — running the AEF Runtime Host, health/readiness signals, graceful shutdown behavior, log format.
- **Deployment Guide** — process/container expectations, configuration surface, the trusted-boundary assumption this GA operates under and what changes when that assumption is later revisited.

## 8. Performance

Production-readiness requirements, framed against RC0's measured baseline (single-caller, ~1.0–1.1s cold boot, ~41.5MB idle working set):

- **Scalability / Concurrency** — the one genuinely new risk. Every shared singleton (`AiExecutionService`, behaviors, `InMemorySessionStore`, the capability catalog) must be verified safe under real overlapping HTTP requests, not just sequential test execution as today. This is Capability 1's concurrency audit, verified again under Capability 2's completion gate with an actual concurrent-load test.
- **Reliability** — graceful shutdown drains in-flight requests; a crashed/unreachable AI provider degrades to honest `Failed` responses, never a hang or a fabricated result (consistent with the runtime's existing "escalate, never fabricate" discipline).
- **Startup** — the host's cold-boot budget should not regress materially from RC0's ~1.0–1.1s; CLI's per-invocation startup (a new cost REST doesn't have, since REST pays it once) should be measured and kept small enough for scripted/Automation use.
- **Memory** — the long-running host's working set must stay stable under sustained request load (no leak from the host's longer lifetime vs. RC0's short-lived process model).
- **Latency** — request latency at the channel layer should add negligible overhead over the pipeline's own execution time (RC0 measured 533–711ms for a real repository-intelligence request) — the adapter is a translation layer, not a bottleneck.
- **Recovery** — process-level restart behavior on crash is a deployment decision (documented in the Deployment Guide), not a new mechanism this program builds.

## 9. Release Readiness

Objective gates, all required before GA-3 is called done:

- [ ] **Architecture Complete** — this document approved; each Capability's own design spec written and approved, citing this document and ADR-0025 rather than re-deriving them.
- [ ] **Channels Complete** — CLI and REST both implemented, each meeting its Capability's completion gate (Section 4).
- [ ] **Documentation Complete** — every artifact in Section 7 exists.
- [ ] **Performance Validated** — every item in Section 8 measured and recorded, concurrency audit finding resolved (not just documented as a risk).
- [ ] **Regression Complete** — the full existing suite (324+ tests through RC0/GA-1/GA-2) plus every new Capability's tests green together.
- [ ] **Engineering Execution Validated** — a real, non-simulated engineering request executes end-to-end through both CLI and REST against a real repository (matching RC0's own Ferret dogfooding precedent), including at least one concurrent-request scenario over REST.
- [ ] **Security Seam Validated** — the trusted-default implementation works, and the extension point is demonstrated (e.g. a test double proving a non-trusted-default implementation could be substituted without touching `Runtime.Agent`), not merely asserted in documentation.

## Review

- ✓ `Runtime.Agent` remains the only execution entry point — both CLI and REST are adapters over it; `EngineeringExecutionPipeline` is never referenced by either.
- ✓ No architectural boundaries violated — ADR-0025's dependency direction is extended, not amended, to the two new channel projects; enforced by an architecture fitness test, not convention alone.
- ✓ Execution channels remain independent — CLI and REST share only `Runtime.Agent`'s contracts; neither depends on the other, and the AEF Runtime Host hosts REST without CLI having any relationship to that host.
- ✓ Engineering Programs remain execution-independent — Section 6 defines orchestration entirely in terms of the existing contracts; no Ferret/Garuda-specific code anywhere in this program.
- ✓ Product execution remains provider-neutral — nothing in this program touches `Runtime.Ai`'s provider abstraction; AI-provider neutrality (GA-1) is untouched.
- ✓ GA-3 supports Ferret, Garuda, and future Engineering Programs — REST's versioned contract plus `ProjectProfileId`-scoped requests is the entire onboarding mechanism for a new program, with no per-program code.
