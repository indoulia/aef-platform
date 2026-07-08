# P2 — Engineering Execution Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire a real, end-to-end "submit an engineering request → get a structured result" pipeline through the existing `aef-platform` Runtime, reusing the already-working Orchestrator/Host/Registry machinery wherever it already does the job, and closing only the genuine gaps: a live-Activation blocker, a Conversation Manager routing/handoff layer, per-invocation binding for the already-built Delivery Execution Behaviors, and the pipeline/result types themselves.

**Architecture:** `EngineeringExecutionPipeline` is the new composition point. It runs `SKL-EXE-0021` (routing, deterministic keyword classifier honoring `PCT-EXE-0021`'s I/O shape) through the existing `SkillHost`, dispatches to whichever existing capability the routing result names — `FeatureLifecycleOrchestrator` (`WFL-DEL-0001`), `EpicLifecycleOrchestrator` (`WFL-DEL-0004`), or the newly-wired Delivery Execution skill trio (`SKL-EXE-0018/19/20`) — then runs `SKL-EXE-0022` (handoff/followup) and returns a structured `ExecutionResult`. Nothing about the existing one-orchestrator-per-workflow pattern changes — per the architecture research, that pattern is D10's own design, not a shortcut. `AGT-EXE-0012/0013/0014` (Engineering Planner / Specification Reviewer / Validation Agent) have zero code or deterministic-algorithm precedent anywhere and are explicitly out of scope for this prompt (documented, not silently skipped) — see Known Out-of-Scope Gap below.

**Tech Stack:** unchanged from P1 — .NET 8, xUnit 2.5.3 / Microsoft.NET.Test.Sdk 17.8.0.

## Global Constraints

- Reuse first, extend second, create new only when the capability genuinely does not exist. Do not rename, replace, or redesign `IRegistry`, `RuntimeCatalog`, `AgentHost`/`SkillHost`/`StrategyHost`, `FeatureLifecycleOrchestrator`/`EpicLifecycleOrchestrator`, `RuntimeExecutionContext`/`ContextSeed`, `RuntimeBootstrapper`, or any existing `DomainContent` type.
- Do not build a generic Workflow-definition interpreter or a "smart" classifying Orchestrator — per the architecture research (D10 §1.2, `docs/discovery/orchestration/01-orchestrator-design.md`), a Workflow Id is always supplied as input to an Orchestrator; classification is Conversation Manager's job (`SKL-EXE-0021`), one layer above.
- `IBehavior.Execute(RuntimeExecutionContext)` stays exactly as defined — never change its signature. Any Behavior needing per-call input reads it from `context.Tier3SkillContent` (the existing, already-used extension point — see `ContextSeed`'s `WorkItemId` precedent).
- Every new/changed `.csproj` stays net8.0, `Nullable`/`ImplicitUsings` enabled, matching existing conventions.
- Whole solution (`AefPlatform.sln`) must build and all tests pass after every commit.
- No placeholder implementations, no TODOs.
- `AGT-EXE-0012`, `AGT-EXE-0013`, `AGT-EXE-0014` and their Skills/Contracts (`SKL-EXE-0023`-`0029`, `PCT-EXE-0023`-`0029`) are explicitly NOT implemented or registered by this plan — document this as a known, deliberate gap (mirroring how P1 documented the Epic 0005 gap), not a silent omission.

## Gap Analysis

| Capability | Existing Implementation | Gap | Action |
|---|---|---|---|
| Engineering request intake / context creation / validation / lifecycle / result generation | `ExecutionContextManager` creates/removes a `RuntimeExecutionContext` per Agent/Strategy instance only; nothing accepts a raw external request | No unified entry point | **Implement** `EngineeringExecutionPipeline` |
| **Live Activation at all** (discovered during research, not in the original prompt) | `GovernanceBoundary.CheckActivation` fails closed (`RejectedNoPolicyResolver`) whenever `IPolicyResolver` is null; `src/Host/Program.cs` boots `RuntimeBootstrapper.Boot()` with no `PolicyResolver` configured | Every Agent/Strategy activation through the live Host is currently rejected — P1 only ever proved Registry registration, never live Activation | **Implement** `PermissiveActivationPolicyResolver` using the Kernel's own pluggable `IPolicyResolver` extension point (exactly as designed for this — "a Service registers itself against this Kernel-defined contract later") |
| Conversation Manager (lifecycle/context/correlation id/state/history) | Zero code presence anywhere. Architecturally specified (`AGT-EXE-0011`, `SKL-EXE-0021/0022`, `PCT-EXE-0021/0022`) as deliberately storage-agnostic — the design doc explicitly declines to specify persistence mechanics | No conversation type or routing/handoff behavior exists | **Implement** `ConversationContext` + `ConversationIntakeAndRoutingBehavior` (SKL-EXE-0021) + `ConversationHandoffAndFollowupBehavior` (SKL-EXE-0022) |
| Orchestrator: classify/select/dispatch/collect/handle failures | `FeatureLifecycleOrchestrator`/`EpicLifecycleOrchestrator`: correct, workflow-bound-at-construction, per D10 design | Classification/dispatch-by-routing-result doesn't exist yet; each Orchestrator only knows its own Workflow | **Reuse** both Orchestrators unmodified; **Implement** the pipeline's own dispatch step (a routing-result → Orchestrator-instance lookup), not a new Orchestrator type |
| Agent Dispatch (discovery/selection/execution/result collection) | `AgentHost.Activate`, `IAgentAllocator`, `GovernanceBoundary` (Registered+Active check), `RuntimeCatalog.Query`/`All()` all already exist and already fail closed on an unregistered/inactive Agent | "Runtime must execute registered agents, not hard-coded logic" is already true structurally once Activation itself works (the blocker above) | **Reuse** entirely once the PolicyResolver blocker is fixed |
| Skill Execution (lookup/invocation/context/structured outputs) | `SkillHost.Invoke` already does Registry lookup + `IBehavior` invocation | None beyond wrapping results into the new `ExecutionResult` | **Reuse** |
| Contract Execution | `PCT-EXE-*` are markdown I/O-shape docs only; no runtime contract-validation exists anywhere | No generic contract engine exists; building one (e.g. JSON-schema validation) would be new, speculative infrastructure no Epic has ever needed | **Reuse** each Delivery Behavior's own already-typed `Outcome` (`ImplementationOutcome`/`ValidationOutcome`/`DocumentationOutcome`, each already matching its `PCT-EXE-0018/19/20` shape) as the de facto contract check — the pipeline surfaces `IsEscalated`/`Recorded` into `ExecutionResult`, no new validator class |
| Workflow Execution (discovery/step execution/context propagation/result aggregation/failure handling) | `StrategyHost`/`SessionTransitions`/both Orchestrators already do this for `WFL-DEL-0001`/`WFL-DEL-0004` | No unified wrapper turning an Orchestrator's `OrchestrationStartResult` into a structured pipeline result | **Implement** the pipeline's result-aggregation step only |
| Runtime Execution Context (conversation/request/workflow/agent/state/outputs/diagnostics) | `RuntimeExecutionContext` is a narrower, existing, four-tier per-instance concept — must not be redefined | A session/conversation-level context is a distinct, new concept | **Implement** `EngineeringExecutionContext` as a new type (composition, not a redefinition of `RuntimeExecutionContext`) |
| Delivery Execution Integration (`SKL-EXE-0018/19/20`, `AGT-EXE-0010`) | `DeliveryImplementationBehavior`/`DeliveryValidationBehavior`/`DeliveryDocumentationBehavior` already implement `IBehavior` correctly, but are constructor-bound to one fixed Request — `SkillHost` binds exactly one `IBehavior` instance per `skillId` at Boot time, so they can't be re-invoked with different requests as-is (the exact gap P1's README/plan flagged) | Per-invocation request binding is missing | **Implement** three thin adapter Behaviors that read their Request from `context.Tier3SkillContent` at `Execute()` time, then delegate to the existing Behaviors unchanged |
| `AGT-EXE-0012`/`0013`/`0014` (Engineering Planner / Spec Reviewer / Validation Agent) | Zero code; their Skills are specified as free-text/NL interpretation with no deterministic-algorithm precedent anywhere in this codebase, and building three more full agent skill-sets is a scope order of magnitude beyond this prompt's other items | Real, but disproportionate — implementing them shallowly would produce hollow/fake registrations | **Document as an explicit, deliberate out-of-scope gap** (P2 §9 says "where appropriate"; forcing this in would repeat the exact "fake it" mistake P1 was praised for avoiding) |
| Structured Results | None; no governed schema exists for a generic execution-result envelope | Genuine new, implementation-only DTO | **Implement** `ExecutionResult` |
| Automated Tests | N/A | New pipeline needs coverage | **Implement** |

## Architecture Decisions (implementation-only)

1. **`PermissiveActivationPolicyResolver`** (`src/Host/`): authorizes any `ActivationRequest` reaching it. By the time `GovernanceBoundary.CheckActivation` calls `IPolicyResolver.Resolve`, it has *already* confirmed the definition is Registered+Active — no other Policy engine/schema exists anywhere in the architecture yet, so this resolver adds no new restriction, it only turns off the Kernel's deliberate "no resolver configured" fail-closed default. This mirrors the `AllowAllPolicyResolver` test double already used throughout `Kernel.Tests`, promoted to a real, named production class.
2. **Delivery Execution adapters read their Request from `RuntimeExecutionContext.Tier3SkillContent`** rather than changing `IBehavior`'s signature or `SkillHost`'s one-instance-per-skillId binding model. This is the same extension point `ContextSeed.WorkItemId` already established a precedent for (Epic 0005, Story S33): additive, optional, doesn't touch any existing call site.
3. **Conversation Manager types stay decoupled from `Runtime.Execution`.** `HandoffRequest` takes a primitive `DispatchOutcomeSummary` (bool Success, string? ExecutedCapabilityId, diagnostics), not the pipeline's own `ExecutionResult` — so `DomainContent.ConversationManagement` has no dependency on `Runtime.Execution`, only the reverse.
4. **`ExecutedAgentId` on `ExecutionResult` is populated only for direct Skill dispatches** (Delivery Execution), not for Orchestrator-dispatched Workflows — both `FeatureLifecycleOrchestrator`/`EpicLifecycleOrchestrator` deliberately keep their target Agent Id `private const`, and reaching into that would break encapsulation for no functional gain; `ExecutedWorkflowId` alone identifies the dispatch for the Workflow case.
5. **No new user-facing input surface** (HTTP endpoint, CLI/REPL) is added. P2's own DoD item ("engineering requests can be executed end-to-end") is satisfied by proving `EngineeringExecutionPipeline` is resolvable from the live Host's DI container and executes correctly against the real booted Registry — inventing a request-intake transport isn't asked for anywhere in the prompt and would be speculative.

## File Structure

- `src/Host/PermissiveActivationPolicyResolver.cs` — **create**.
- `src/Host/Program.cs` — **modify**: configure the resolver, build the `behaviors` dictionary, register `EngineeringExecutionPipeline` in DI.
- `src/Host/EngineeringArchitectureManifest.cs` — **modify**: +7 entries.
- `src/Runtime/DomainContent/ConversationManagement/ConversationContext.cs` — **create**.
- `src/Runtime/DomainContent/ConversationManagement/RoutingTypes.cs` — **create** (`RoutingOutcome`, `RoutingRequest`, `RoutingResult`).
- `src/Runtime/DomainContent/ConversationManagement/ConversationIntakeAndRoutingBehavior.cs` — **create** (SKL-EXE-0021).
- `src/Runtime/DomainContent/ConversationManagement/HandoffTypes.cs` — **create** (`DispatchOutcomeSummary`, `HandoffRequest`, `HandoffResult`).
- `src/Runtime/DomainContent/ConversationManagement/ConversationHandoffAndFollowupBehavior.cs` — **create** (SKL-EXE-0022).
- `src/Runtime/DomainContent/DeliveryExecution/DeliveryImplementationSkillBehavior.cs` — **create**.
- `src/Runtime/DomainContent/DeliveryExecution/DeliveryValidationSkillBehavior.cs` — **create**.
- `src/Runtime/DomainContent/DeliveryExecution/DeliveryDocumentationSkillBehavior.cs` — **create**.
- `src/Runtime/Execution/EngineeringRequest.cs` — **create**.
- `src/Runtime/Execution/ExecutionResult.cs` — **create**.
- `src/Runtime/Execution/EngineeringExecutionContext.cs` — **create**.
- `src/Runtime/Execution/EngineeringExecutionPipeline.cs` — **create**.
- `tests/Kernel.Tests/Governance/PermissiveActivationPolicyResolverTests.cs` — wait, this resolver lives in `src/Host`, so test goes in `tests/Host.Tests/PermissiveActivationPolicyResolverTests.cs` — **create**.
- `tests/Host.Tests/RuntimeArchitectureLoaderTests.cs` — **modify**: manifest count 17→24.
- `tests/Runtime.Tests/DomainContent/ConversationManagement/ConversationIntakeAndRoutingBehaviorTests.cs` — **create**.
- `tests/Runtime.Tests/DomainContent/ConversationManagement/ConversationHandoffAndFollowupBehaviorTests.cs` — **create**.
- `tests/Runtime.Tests/DomainContent/DeliveryExecution/DeliverySkillBehaviorAdapterTests.cs` — **create**.
- `tests/Runtime.Tests/Execution/EngineeringExecutionPipelineTests.cs` — **create**.
- `tests/Host.Tests/EngineeringExecutionPipelineHostIntegrationTests.cs` — **create** (live-Host end-to-end proof).
- `README.md` — **modify**: P2 status entry.

---

## Task 1: Fix the live-Activation blocker — `PermissiveActivationPolicyResolver`

**Files:** Create `src/Host/PermissiveActivationPolicyResolver.cs`; modify `src/Host/Program.cs`; create `tests/Host.Tests/PermissiveActivationPolicyResolverTests.cs`.

**Interfaces:** Implements `Kernel.Governance.IPolicyResolver.Resolve(ActivationRequest) -> PolicyResolution`.

```csharp
using Kernel.Governance;

namespace AefPlatform.Host;

// GovernanceBoundary.CheckActivation only calls IPolicyResolver.Resolve after
// it has already confirmed the ActivationRequest's DefinitionId is
// Registered+Active — no other Policy engine exists anywhere in the
// architecture yet, so this resolver adds no restriction beyond that; it
// exists solely to opt out of the Kernel's deliberate "no resolver
// configured" fail-closed default (Kernel.Governance.IPolicyResolver's own
// doc comment). Promotes the AllowAllPolicyResolver test double already
// used throughout Kernel.Tests into a real, named production class.
public sealed class PermissiveActivationPolicyResolver : IPolicyResolver
{
    public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
}
```

In `Program.cs`, change:
```csharp
builder.Services.AddSingleton(_ => RuntimeBootstrapper.Boot());
```
to (final shape depends on Task 9's `behaviors` dictionary — for this task, only the resolver changes):
```csharp
builder.Services.AddSingleton(_ => RuntimeBootstrapper.Boot(
    new RuntimeConfiguration(PolicyResolver: new PermissiveActivationPolicyResolver())));
```
(`using Runtime.Bootstrapping;` already present.)

Test (`tests/Host.Tests/PermissiveActivationPolicyResolverTests.cs`):
```csharp
using AefPlatform.Host;
using Kernel.Governance;

namespace Host.Tests;

public class PermissiveActivationPolicyResolverTests
{
    [Fact]
    public void Resolve_AnyRequest_Authorizes()
    {
        var resolver = new PermissiveActivationPolicyResolver();

        var result = resolver.Resolve(new ActivationRequest("any-id"));

        Assert.True(result.Authorized);
    }
}
```

- [ ] Write the failing test, confirm it fails to compile (`PermissiveActivationPolicyResolver` doesn't exist).
- [ ] Implement the resolver and the `Program.cs` change above.
- [ ] `dotnet test tests/Host.Tests/Host.Tests.csproj` — new test passes.
- [ ] `dotnet test AefPlatform.sln` — full suite green.
- [ ] Commit: `fix(host): configure a PolicyResolver so live Activation is no longer rejected by default`.

---

## Task 2: Extend the Engineering Architecture Manifest

**Files:** Modify `src/Host/EngineeringArchitectureManifest.cs`; modify `tests/Host.Tests/RuntimeArchitectureLoaderTests.cs`.

Add to `EngineeringArchitectureManifest.KnownArtifacts` (all Domain `"Execution"`, matching every existing Skill/Agent entry's convention):
```csharp
new ArchitectureArtifact("AGT-EXE-0010", "Execution"),
new ArchitectureArtifact("SKL-EXE-0018", "Execution"),
new ArchitectureArtifact("SKL-EXE-0019", "Execution"),
new ArchitectureArtifact("SKL-EXE-0020", "Execution"),
new ArchitectureArtifact("AGT-EXE-0011", "Execution"),
new ArchitectureArtifact("SKL-EXE-0021", "Execution"),
new ArchitectureArtifact("SKL-EXE-0022", "Execution"),
```
(17 + 7 = 24 total.)

In `EngineeringArchitectureManifestTests.KnownArtifacts_IncludesEveryEpicAlreadyProvenRegistrableInThisRepo`, add:
```csharp
Assert.Contains("AGT-EXE-0010", ids);
Assert.Contains("SKL-EXE-0018", ids);
Assert.Contains("SKL-EXE-0019", ids);
Assert.Contains("SKL-EXE-0020", ids);
Assert.Contains("AGT-EXE-0011", ids);
Assert.Contains("SKL-EXE-0021", ids);
Assert.Contains("SKL-EXE-0022", ids);
```
And in `RuntimeArchitectureLoaderTests.Load_TheRealManifest_RegistersEveryKnownArtifactCleanly` — no change needed, it already asserts against `KnownArtifacts.Count` dynamically.

- [ ] Add the 7 entries + assertions (RED first: assertions fail against the old 17-entry list).
- [ ] Confirm GREEN: `dotnet test tests/Host.Tests/Host.Tests.csproj`.
- [ ] Full suite green.
- [ ] Commit: `feat(host): extend the manifest with Delivery Execution and Conversation Manager artifacts`.

**Note:** registering these Ids now (before their Behaviors exist) will make `RuntimeArchitectureLoader` register them successfully (Registry registration doesn't require a bound Behavior — only `SkillHost.Invoke` does, at call time). Behaviors are wired in Task 9's `Program.cs` update, once they exist.

---

## Task 3: Conversation types — `ConversationContext`, routing types, handoff types

**Files:** Create `src/Runtime/DomainContent/ConversationManagement/ConversationContext.cs`, `RoutingTypes.cs`, `HandoffTypes.cs`.

```csharp
// ConversationContext.cs
namespace Runtime.DomainContent.ConversationManagement;

// The "runtime conversation context" AGT-EXE-0011's own design doc
// (docs/superpowers/specs/2026-07-07-aef-assistant-conversation-manager-
// design.md) defines: host-supplied, not a governed artifact or Context
// type. The design doc deliberately declines to specify storage/
// persistence mechanics - this type carries exactly the named fields and
// nothing more.
public sealed record ConversationContext(
    string ConversationId,
    IReadOnlyList<string> AskedQuestions,
    IReadOnlyList<string> AnsweredQuestions,
    string? ActiveWorkflowId,
    string? RoadmapItemInFocus,
    bool PendingApproval,
    bool RepositoryContextAvailable)
{
    public static ConversationContext New(string conversationId) =>
        new(conversationId, Array.Empty<string>(), Array.Empty<string>(), null, null, false, false);
}
```

```csharp
// RoutingTypes.cs — mirrors PCT-EXE-0021's Input/Output shape
namespace Runtime.DomainContent.ConversationManagement;

public enum RoutingOutcome
{
    Routed,
    NeedsClarification,
    ContextDiscoveryRequired
}

public sealed record RoutingRequest(string UserRequest, ConversationContext ConversationContext);

public sealed record RoutingResult(
    RoutingOutcome Outcome,
    string? TargetCapabilityId,
    IReadOnlyList<string>? ClarificationQuestions,
    string? DiscoveryTarget)
{
    public static RoutingResult Routed(string targetCapabilityId) =>
        new(RoutingOutcome.Routed, targetCapabilityId, null, null);

    public static RoutingResult NeedsClarification(IReadOnlyList<string> questions) =>
        new(RoutingOutcome.NeedsClarification, null, questions, null);

    public static RoutingResult ContextDiscoveryRequired(string discoveryTarget) =>
        new(RoutingOutcome.ContextDiscoveryRequired, null, null, discoveryTarget);
}
```

```csharp
// HandoffTypes.cs — mirrors PCT-EXE-0022's Input/Output shape; deliberately
// primitive (not Runtime.Execution.ExecutionResult) so ConversationManagement
// has no dependency on the Execution namespace (Architecture Decision 3).
namespace Runtime.DomainContent.ConversationManagement;

public sealed record DispatchOutcomeSummary(bool Success, string? ExecutedCapabilityId, IReadOnlyList<string> Diagnostics);

public sealed record HandoffRequest(RoutingResult RoutingResult, DispatchOutcomeSummary? DispatchOutcome);

public sealed record HandoffResult(string PresentedResult, string? SuggestedNextStep);
```

No behavior yet — these are pure data types. A minimal test proving `ConversationContext.New` produces the documented empty-state defaults is enough:

`tests/Runtime.Tests/DomainContent/ConversationManagement/ConversationContextTests.cs`:
```csharp
using Runtime.DomainContent.ConversationManagement;

namespace Runtime.Tests.DomainContent.ConversationManagement;

public class ConversationContextTests
{
    [Fact]
    public void New_ProducesEmptyStateDefaults()
    {
        var context = ConversationContext.New("conv-1");

        Assert.Equal("conv-1", context.ConversationId);
        Assert.Empty(context.AskedQuestions);
        Assert.Empty(context.AnsweredQuestions);
        Assert.Null(context.ActiveWorkflowId);
        Assert.False(context.PendingApproval);
        Assert.False(context.RepositoryContextAvailable);
    }
}
```

- [ ] Write the failing test, implement the three files above, confirm GREEN.
- [ ] Full suite green. Commit: `feat(runtime): add Conversation Manager data types (ConversationContext, routing, handoff)`.

---

## Task 4: `ConversationIntakeAndRoutingBehavior` (SKL-EXE-0021)

**Files:** Create `src/Runtime/DomainContent/ConversationManagement/ConversationIntakeAndRoutingBehavior.cs`; create `tests/Runtime.Tests/DomainContent/ConversationManagement/ConversationIntakeAndRoutingBehaviorTests.cs`.

**Interfaces:** Implements `Runtime.Hosts.Skill.IBehavior`. Reads a `RoutingRequest` from `context.Tier3SkillContent["RoutingRequest"]`. Produces `RoutingResult`.

```csharp
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.ConversationManagement;

// The executable realization of SKL-EXE-0021 (framework/execution/skills/
// conversation-intake-and-routing.md)'s Behavior. The governed Skill spec
// is written as free-text/NL interpretation ("no fixed command grammar
// required") intended for LLM-driven execution - no LLM integration point
// exists anywhere in this Runtime today, and building one is out of this
// Prompt's scope. This is therefore a deterministic keyword classifier
// honoring PCT-EXE-0021's Input/Output contract shape exactly, limited to
// the capabilities this Runtime can actually dispatch to (WFL-DEL-0001,
// WFL-DEL-0004, the Delivery Execution skill trio) - it never routes to a
// capability with no executable realization, and asks for clarification
// rather than guess.
public sealed class ConversationIntakeAndRoutingBehavior : IBehavior
{
    private static readonly (string Keyword, string CapabilityId)[] KnownCapabilities =
    {
        ("epic", "WFL-DEL-0004"),
        ("feature", "WFL-DEL-0001"),
        ("deliver", "SKL-EXE-0018"),
        ("delivery", "SKL-EXE-0018"),
    };

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("RoutingRequest", out var raw) || raw is not RoutingRequest request)
        {
            return RoutingResult.NeedsClarification(new[] { "No request text was supplied to route." });
        }

        var lowered = request.UserRequest.ToLowerInvariant();
        foreach (var (keyword, capabilityId) in KnownCapabilities)
        {
            if (lowered.Contains(keyword))
            {
                return RoutingResult.Routed(capabilityId);
            }
        }

        return RoutingResult.NeedsClarification(new[]
        {
            "Which capability should handle this request? Supported: feature, epic, delivery."
        });
    }
}
```

Tests (construct a bare `RuntimeExecutionContext` directly, matching existing `SkillHostUnitTests`-style usage):
```csharp
using Runtime.Context;
using Runtime.DomainContent.ConversationManagement;

namespace Runtime.Tests.DomainContent.ConversationManagement;

public class ConversationIntakeAndRoutingBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(RoutingRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["RoutingRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Intake", content);
    }

    [Theory]
    [InlineData("Implement the new feature for X", "WFL-DEL-0001")]
    [InlineData("Start the epic for repository onboarding", "WFL-DEL-0004")]
    [InlineData("Deliver the fix for the bug", "SKL-EXE-0018")]
    public void Execute_KnownKeyword_RoutesToTheMatchingCapability(string userRequest, string expectedCapabilityId)
    {
        var behavior = new ConversationIntakeAndRoutingBehavior();
        var context = ContextWith(new RoutingRequest(userRequest, ConversationContext.New("conv-1")));

        var result = (RoutingResult)behavior.Execute(context)!;

        Assert.Equal(RoutingOutcome.Routed, result.Outcome);
        Assert.Equal(expectedCapabilityId, result.TargetCapabilityId);
    }

    [Fact]
    public void Execute_UnknownRequest_NeedsClarification()
    {
        var behavior = new ConversationIntakeAndRoutingBehavior();
        var context = ContextWith(new RoutingRequest("What time is it?", ConversationContext.New("conv-1")));

        var result = (RoutingResult)behavior.Execute(context)!;

        Assert.Equal(RoutingOutcome.NeedsClarification, result.Outcome);
        Assert.NotEmpty(result.ClarificationQuestions!);
    }

    [Fact]
    public void Execute_NoRoutingRequestSupplied_NeedsClarification()
    {
        var behavior = new ConversationIntakeAndRoutingBehavior();
        var context = ContextWith(null);

        var result = (RoutingResult)behavior.Execute(context)!;

        Assert.Equal(RoutingOutcome.NeedsClarification, result.Outcome);
    }
}
```

- [ ] RED → implement → GREEN → full suite green.
- [ ] Commit: `feat(runtime): implement SKL-EXE-0021 Conversation Intake and Routing`.

---

## Task 5: `ConversationHandoffAndFollowupBehavior` (SKL-EXE-0022)

**Files:** Create `src/Runtime/DomainContent/ConversationManagement/ConversationHandoffAndFollowupBehavior.cs`; create `tests/Runtime.Tests/DomainContent/ConversationManagement/ConversationHandoffAndFollowupBehaviorTests.cs`.

```csharp
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.ConversationManagement;

// The executable realization of SKL-EXE-0022 (framework/execution/skills/
// conversation-handoff-and-followup.md)'s Behavior: translates a routing
// decision plus (if any) its dispatch outcome into plain-language
// presentation, per PCT-EXE-0022's Output shape (presented_result,
// pause_reason equivalent, suggested_next_step). Never touches
// Runtime.Execution types directly (Architecture Decision 3).
public sealed class ConversationHandoffAndFollowupBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("HandoffRequest", out var raw) || raw is not HandoffRequest handoff)
        {
            return new HandoffResult("No handoff payload was supplied.", null);
        }

        if (handoff.RoutingResult.Outcome == RoutingOutcome.NeedsClarification)
        {
            var questions = string.Join(" ", handoff.RoutingResult.ClarificationQuestions ?? Array.Empty<string>());
            return new HandoffResult($"I need more information: {questions}", "Answer the clarification question(s) above and resubmit.");
        }

        if (handoff.DispatchOutcome is null)
        {
            return new HandoffResult("Routing succeeded but no dispatch outcome was supplied.", null);
        }

        return handoff.DispatchOutcome.Success
            ? new HandoffResult($"Completed via {handoff.DispatchOutcome.ExecutedCapabilityId}.", "No further action needed.")
            : new HandoffResult($"Execution failed: {string.Join("; ", handoff.DispatchOutcome.Diagnostics)}", "Review the diagnostics and retry.");
    }
}
```

Tests: mirror the shape of Task 4's tests — one case per branch (no payload, needs-clarification, successful dispatch, failed dispatch). Use the same `RuntimeExecutionContext` construction pattern as Task 4, with `Tier3SkillContent["HandoffRequest"]`.

- [ ] RED → implement → GREEN → full suite green.
- [ ] Commit: `feat(runtime): implement SKL-EXE-0022 Conversation Handoff and Followup`.

---

## Task 6: Delivery Execution SkillHost adapters

**Files:** Create `src/Runtime/DomainContent/DeliveryExecution/DeliveryImplementationSkillBehavior.cs`, `DeliveryValidationSkillBehavior.cs`, `DeliveryDocumentationSkillBehavior.cs`; create `tests/Runtime.Tests/DomainContent/DeliveryExecution/DeliverySkillBehaviorAdapterTests.cs`.

**Interfaces:** Each implements `IBehavior`; each reads its own Request type from `context.Tier3SkillContent`, then delegates to the existing, unmodified `DeliveryImplementationBehavior`/`DeliveryValidationBehavior`/`DeliveryDocumentationBehavior`.

```csharp
// DeliveryImplementationSkillBehavior.cs
using Runtime.Context;
using Runtime.Hosts.Skill;
using Runtime.Integration.DeliveryExecution;

namespace Runtime.DomainContent.DeliveryExecution;

// SkillHost binds exactly one IBehavior instance per skillId at Boot time
// (Runtime.Bootstrapping.RuntimeBootstrapper), but DeliveryImplementationBehavior
// is constructor-bound to one fixed ImplementationRequest - this adapter
// reads the real per-invocation request from context.Tier3SkillContent
// (Architecture Decision 2) so SKL-EXE-0018 can be re-invoked through
// SkillHost.Invoke with a different request each call, without changing
// IBehavior, SkillHost, or DeliveryImplementationBehavior itself.
public sealed class DeliveryImplementationSkillBehavior : IBehavior
{
    private readonly Func<string, IIsolatedChangeVerifier> _verifierFactory;

    public DeliveryImplementationSkillBehavior(Func<string, IIsolatedChangeVerifier>? verifierFactory = null)
    {
        _verifierFactory = verifierFactory ?? (repositoryRoot => new LocalGitChangeVerifier(repositoryRoot, "main"));
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("ImplementationRequest", out var rawRequest) || rawRequest is not ImplementationRequest request)
        {
            return ImplementationOutcome.Escalated("No ImplementationRequest was supplied in the execution context.");
        }

        if (!context.Tier3SkillContent.TryGetValue("RepositoryRoot", out var rawRoot) || rawRoot is not string repositoryRoot)
        {
            return ImplementationOutcome.Escalated("No RepositoryRoot was supplied in the execution context.");
        }

        var verifier = _verifierFactory(repositoryRoot);
        return new DeliveryImplementationBehavior(request, verifier).Execute(context);
    }
}
```

```csharp
// DeliveryValidationSkillBehavior.cs
using Runtime.Context;
using Runtime.Hosts.Skill;
using Runtime.Integration.DeliveryExecution;

namespace Runtime.DomainContent.DeliveryExecution;

public sealed class DeliveryValidationSkillBehavior : IBehavior
{
    private readonly IVerificationRunner _runner;

    public DeliveryValidationSkillBehavior(IVerificationRunner? runner = null)
    {
        _runner = runner ?? new ProcessVerificationRunner();
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("ValidationRequest", out var raw) || raw is not ValidationRequest request)
        {
            return ValidationOutcome.Escalated("No ValidationRequest was supplied in the execution context.");
        }

        return new DeliveryValidationBehavior(request, _runner).Execute(context);
    }
}
```

```csharp
// DeliveryDocumentationSkillBehavior.cs
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.DeliveryExecution;

public sealed class DeliveryDocumentationSkillBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("DocumentationRequest", out var raw) || raw is not DocumentationRequest request)
        {
            return DocumentationOutcome.Escalated("No DocumentationRequest was supplied in the execution context.");
        }

        return new DeliveryDocumentationBehavior(request).Execute(context);
    }
}
```

Tests — one class covering all three adapters, each with a "request present → delegates correctly" case and a "request missing → escalates" case. For the Implementation adapter, use a fake `IIsolatedChangeVerifier` (or a real temp git repo, mirroring `Epic0005EndToEndDeliveryDogfoodingTests`'s pattern) — a fake is simpler and sufficient here since `DeliveryImplementationBehavior` itself is already fully tested elsewhere; this test only needs to prove the adapter wires the request through correctly:

```csharp
using Runtime.Context;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.Integration.DeliveryExecution;

namespace Runtime.Tests.DomainContent.DeliveryExecution;

public class DeliverySkillBehaviorAdapterTests
{
    private static RuntimeExecutionContext ContextWith(string key, object? value)
    {
        var content = value is null ? new Dictionary<string, object>() : new Dictionary<string, object> { [key] = value };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Implementation", content);
    }

    [Fact]
    public void ImplementationAdapter_NoRequestSupplied_Escalates()
    {
        var behavior = new DeliveryImplementationSkillBehavior();

        var outcome = (ImplementationOutcome)behavior.Execute(ContextWith("Other", "x"))!;

        Assert.True(outcome.IsEscalated);
    }

    [Fact]
    public void ImplementationAdapter_ValidRequest_DelegatesToDeliveryImplementationBehavior()
    {
        var behavior = new DeliveryImplementationSkillBehavior(_ => new AlwaysVerifiedVerifier());
        var request = new ImplementationRequest("story-1", "feature/x", new[] { "abc123" }, "plan");
        var content = new Dictionary<string, object>
        {
            ["ImplementationRequest"] = request,
            ["RepositoryRoot"] = "C:\\fake-repo",
        };
        var context = new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Implementation", content);

        var outcome = (ImplementationOutcome)behavior.Execute(context)!;

        Assert.False(outcome.IsEscalated);
        Assert.Equal("story-1", outcome.LogEntry!.StoryReferenceId);
    }

    [Fact]
    public void ValidationAdapter_NoRequestSupplied_Escalates()
    {
        var behavior = new DeliveryValidationSkillBehavior();

        var outcome = (ValidationOutcome)behavior.Execute(ContextWith("Other", "x"))!;

        Assert.True(outcome.IsEscalated);
    }

    [Fact]
    public void DocumentationAdapter_NoRequestSupplied_Escalates()
    {
        var behavior = new DeliveryDocumentationSkillBehavior();

        var outcome = (DocumentationOutcome)behavior.Execute(ContextWith("Other", "x"))!;

        Assert.True(outcome.IsEscalated);
    }

    private sealed class AlwaysVerifiedVerifier : IIsolatedChangeVerifier
    {
        public bool BranchExists(string branchName) => true;
        public bool IsDefaultBranch(string branchName) => false;
        public bool CommitExistsOnBranch(string branchName, string commitSha) => true;
    }
}
```

- [ ] RED → implement all three adapters → GREEN → full suite green.
- [ ] Commit: `feat(runtime): adapt Delivery Execution Behaviors for per-invocation SkillHost binding`.

---

## Task 7: `ExecutionResult`, `EngineeringRequest`, `EngineeringExecutionContext`

**Files:** Create `src/Runtime/Execution/EngineeringRequest.cs`, `ExecutionResult.cs`, `EngineeringExecutionContext.cs`.

```csharp
// EngineeringRequest.cs
namespace Runtime.Execution;

// The external entry-point payload. Payload is a deliberately open,
// on-demand-fetched bag (mirroring RuntimeExecutionContext.Tier3SkillContent's
// own "fetched on demand" design) rather than a rigid closed schema, since
// different routed capabilities (Delivery Execution vs. a Workflow trigger)
// need different structured input.
public sealed record EngineeringRequest(
    string UserRequest,
    string ProjectProfileId,
    IReadOnlyDictionary<string, object>? Payload = null);
```

```csharp
// ExecutionResult.cs
namespace Runtime.Execution;

// The structured execution result P2 requires. No governed PCT-/SCH-
// schema exists for this generic shape anywhere in the architecture
// (confirmed by research) - this is a genuine, implementation-only DTO.
public sealed record ExecutionResult(
    bool Success,
    string? ExecutedWorkflowId,
    string? ExecutedAgentId,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string>? ValidationResults,
    TimeSpan Duration,
    string? PresentedResult,
    string? SuggestedNextStep);
```

```csharp
// EngineeringExecutionContext.cs
namespace Runtime.Execution;

public enum EngineeringExecutionState
{
    NotStarted,
    Routing,
    Dispatching,
    Completed,
    Failed,
    NeedsClarification
}

// The session/conversation-level context the pipeline threads through one
// request's execution. Deliberately distinct from Runtime.Context.
// RuntimeExecutionContext (the existing, narrower, per-Kernel-Instance
// four-tier concept) - this is a new type, not a redefinition of that one.
public sealed class EngineeringExecutionContext
{
    public required Runtime.DomainContent.ConversationManagement.ConversationContext Conversation { get; init; }
    public required EngineeringRequest Request { get; init; }
    public string? SelectedWorkflowId { get; set; }
    public string? SelectedAgentId { get; set; }
    public EngineeringExecutionState State { get; set; } = EngineeringExecutionState.NotStarted;
    public List<object> Outputs { get; } = new();
    public List<string> Diagnostics { get; } = new();
}
```

No standalone tests needed for these three pure-data files beyond what Task 8's pipeline tests already exercise them through — per the plan's own "don't write a test that asserts nothing" discipline, a construction-only test here would be trivial noise; their behavior is proven end-to-end in Task 8.

- [ ] Implement all three files (no RED/GREEN cycle needed — pure data types with no behavior of their own).
- [ ] `dotnet build AefPlatform.sln` succeeds.
- [ ] Commit: `feat(runtime): add EngineeringRequest, ExecutionResult, EngineeringExecutionContext`.

---

## Task 8: `EngineeringExecutionPipeline`

**Files:** Create `src/Runtime/Execution/EngineeringExecutionPipeline.cs`; create `tests/Runtime.Tests/Execution/EngineeringExecutionPipelineTests.cs`.

**Interfaces:**
- Consumes: `RuntimeHandle` (ctor), `EngineeringRequest` (`Execute` parameter).
- Produces: `ExecutionResult Execute(EngineeringRequest request)` — Task 9's `Program.cs` and Task 10's Host integration test both call this exact method.

```csharp
using System.Diagnostics;
using Runtime.Bootstrapping;
using Runtime.Context;
using Runtime.DomainContent.ConversationManagement;
using Runtime.Orchestration;

namespace Runtime.Execution;

// The P2 composition point: routes a raw EngineeringRequest through
// SKL-EXE-0021 (SkillHost), dispatches to whichever existing, unmodified
// capability the routing result names, runs SKL-EXE-0022, and returns a
// structured ExecutionResult. Builds FeatureLifecycleOrchestrator/
// EpicLifecycleOrchestrator itself from the already-booted RuntimeHandle's
// StrategyHost, exactly as every existing test already does - RuntimeHandle
// itself is not modified to expose them (Architecture Decision: minimal,
// non-invasive composition).
public sealed class EngineeringExecutionPipeline
{
    private readonly RuntimeHandle _runtime;
    private readonly FeatureLifecycleOrchestrator _featureOrchestrator;
    private readonly EpicLifecycleOrchestrator _epicOrchestrator;

    public EngineeringExecutionPipeline(RuntimeHandle runtime)
    {
        _runtime = runtime;
        _featureOrchestrator = new FeatureLifecycleOrchestrator(runtime.StrategyHost);
        _epicOrchestrator = new EpicLifecycleOrchestrator(runtime.StrategyHost);
    }

    public ExecutionResult Execute(EngineeringRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var conversation = ConversationContext.New(Guid.NewGuid().ToString("N"));
        var executionContext = new EngineeringExecutionContext { Conversation = conversation, Request = request };

        executionContext.State = EngineeringExecutionState.Routing;
        var routingResult = Route(request, conversation);

        if (routingResult.Outcome == RoutingOutcome.NeedsClarification)
        {
            executionContext.State = EngineeringExecutionState.NeedsClarification;
            executionContext.Diagnostics.AddRange(routingResult.ClarificationQuestions ?? Array.Empty<string>());
            var handoff = Handoff(routingResult, dispatchOutcome: null);
            stopwatch.Stop();
            return new ExecutionResult(
                Success: false, null, null, executionContext.Diagnostics, null, stopwatch.Elapsed,
                handoff.PresentedResult, handoff.SuggestedNextStep);
        }

        executionContext.State = EngineeringExecutionState.Dispatching;
        var dispatch = Dispatch(routingResult, request);
        executionContext.SelectedWorkflowId = dispatch.ExecutedWorkflowId;
        executionContext.SelectedAgentId = dispatch.ExecutedAgentId;
        executionContext.Diagnostics.AddRange(dispatch.Diagnostics);
        executionContext.State = dispatch.Success ? EngineeringExecutionState.Completed : EngineeringExecutionState.Failed;

        var summary = new DispatchOutcomeSummary(dispatch.Success, routingResult.TargetCapabilityId, dispatch.Diagnostics);
        var finalHandoff = Handoff(routingResult, summary);
        stopwatch.Stop();

        return dispatch with { Duration = stopwatch.Elapsed, PresentedResult = finalHandoff.PresentedResult, SuggestedNextStep = finalHandoff.SuggestedNextStep };
    }

    private RoutingResult Route(EngineeringRequest request, ConversationContext conversation)
    {
        var routingContext = new RuntimeExecutionContext(
            "routing-" + conversation.ConversationId,
            new Tier0Context(Array.Empty<string>(), Array.Empty<string>()),
            request.ProjectProfileId,
            "Routing",
            new Dictionary<string, object> { ["RoutingRequest"] = new RoutingRequest(request.UserRequest, conversation) });

        var invocation = _runtime.SkillHost.Invoke(routingContext, "SKL-EXE-0021");
        return invocation.Outcome == Hosts.Agent.SkillInvocationOutcome.Invoked
            ? (RoutingResult)invocation.Output!
            : RoutingResult.NeedsClarification(new[] { invocation.Reason ?? "SKL-EXE-0021 is not invocable." });
    }

    private HandoffResult Handoff(RoutingResult routingResult, DispatchOutcomeSummary? dispatchOutcome)
    {
        var handoffContext = new RuntimeExecutionContext(
            "handoff-" + Guid.NewGuid().ToString("N"),
            new Tier0Context(Array.Empty<string>(), Array.Empty<string>()),
            "default-profile",
            "Handoff",
            new Dictionary<string, object> { ["HandoffRequest"] = new HandoffRequest(routingResult, dispatchOutcome) });

        var invocation = _runtime.SkillHost.Invoke(handoffContext, "SKL-EXE-0022");
        return invocation.Outcome == Hosts.Agent.SkillInvocationOutcome.Invoked
            ? (HandoffResult)invocation.Output!
            : new HandoffResult(invocation.Reason ?? "SKL-EXE-0022 is not invocable.", null);
    }

    private ExecutionResult Dispatch(RoutingResult routingResult, EngineeringRequest request)
    {
        return routingResult.TargetCapabilityId switch
        {
            "WFL-DEL-0001" => FromOrchestration(_featureOrchestrator.Start(new ManualTrigger("WFL-DEL-0001", request.Payload)), "WFL-DEL-0001"),
            "WFL-DEL-0004" => FromOrchestration(_epicOrchestrator.Start(new ManualTrigger("WFL-DEL-0004", request.Payload)), "WFL-DEL-0004"),
            "SKL-EXE-0018" => FromDeliveryExecution(request),
            _ => new ExecutionResult(false, null, null, new[] { $"No executable dispatch target for capability '{routingResult.TargetCapabilityId}'." }, null, TimeSpan.Zero, null, null),
        };
    }

    private static ExecutionResult FromOrchestration(OrchestrationStartResult result, string workflowId)
    {
        var success = result.Outcome == OrchestrationStartOutcome.Started;
        var diagnostics = success ? Array.Empty<string>() : new[] { result.Reason ?? "Orchestrator rejected the trigger." };
        return new ExecutionResult(success, success ? workflowId : null, null, diagnostics, null, TimeSpan.Zero, null, null);
    }

    private ExecutionResult FromDeliveryExecution(EngineeringRequest request)
    {
        var payload = request.Payload ?? new Dictionary<string, object>();
        var skillContext = new RuntimeExecutionContext(
            "delivery-" + Guid.NewGuid().ToString("N"),
            new Tier0Context(Array.Empty<string>(), Array.Empty<string>()),
            request.ProjectProfileId,
            "Implementation",
            payload);

        var invocation = _runtime.SkillHost.Invoke(skillContext, "SKL-EXE-0018");
        if (invocation.Outcome != Hosts.Agent.SkillInvocationOutcome.Invoked)
        {
            return new ExecutionResult(false, null, "AGT-EXE-0010", new[] { invocation.Reason ?? "SKL-EXE-0018 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var outcome = (DomainContent.DeliveryExecution.ImplementationOutcome)invocation.Output!;
        var diagnostics = outcome.IsEscalated ? new[] { outcome.EscalationReason! } : Array.Empty<string>();
        var validationResults = outcome.IsEscalated ? null : new[] { $"Verified commits: {string.Join(", ", outcome.LogEntry!.VerifiedCommitShas)}" };
        return new ExecutionResult(!outcome.IsEscalated, null, "AGT-EXE-0010", diagnostics, validationResults, TimeSpan.Zero, null, null);
    }
}
```

Tests (`tests/Runtime.Tests/Execution/EngineeringExecutionPipelineTests.cs`) — boot a real `RuntimeBootstrapper.Boot()` with an `AllowAllPolicyResolver` test double (mirroring `KernelBootstrappingUnitTests`) and the three new behaviors bound (`SKL-EXE-0021`, `SKL-EXE-0022`, plus `SKL-EXE-0018` for the delivery path), register the needed Ids for each scenario, and cover:

1. `Execute_FeatureRequest_RoutesAndDispatchesToFeatureLifecycleOrchestrator` — register `ORC-EXE-0001`, `WFL-DEL-0001`, `AGT-EXE-0002`, `SKL-EXE-0021`, `SKL-EXE-0022`; request text containing "feature"; assert `Success == true`, `ExecutedWorkflowId == "WFL-DEL-0001"`.
2. `Execute_EpicRequest_RoutesAndDispatchesToEpicLifecycleOrchestrator` — same shape for `WFL-DEL-0004`/`AGT-EXE-0006`.
3. `Execute_UnroutableRequest_ReturnsFailureWithClarificationDiagnostics` — request text "What time is it?"; assert `Success == false`, `Diagnostics` non-empty, `PresentedResult` contains "I need more information".
4. `Execute_DeliveryRequest_DispatchesThroughSkillHostAndReturnsValidationResults` — register `SKL-EXE-0018`, `SKL-EXE-0021`, `SKL-EXE-0022`; bind `DeliveryImplementationSkillBehavior` with a fake always-verified `IIsolatedChangeVerifier` factory; request text "deliver the fix" with `Payload` carrying a valid `ImplementationRequest` + `RepositoryRoot`; assert `Success == true`, `ExecutedAgentId == "AGT-EXE-0010"`, `ValidationResults` non-null.
5. `Execute_WorkflowNotRegistered_ReturnsFailureDiagnostics` — boot without registering `WFL-DEL-0001`/`AGT-EXE-0002`/`ORC-EXE-0001`; request "implement a feature"; assert `Success == false` and a diagnostic mentioning the rejection reason (proves Orchestrator rejection surfaces correctly, not silently).

- [ ] Write all 5 tests RED (compile-fail first, since the class doesn't exist).
- [ ] Implement `EngineeringExecutionPipeline`.
- [ ] All 5 GREEN.
- [ ] Full suite green.
- [ ] Commit: `feat(runtime): implement EngineeringExecutionPipeline`.

---

## Task 9: Wire the live Host — behaviors dictionary, pipeline in DI

**Files:** Modify `src/Host/Program.cs`.

```csharp
using AefPlatform.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runtime.DomainContent.ConversationManagement;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.Execution;
using Runtime.Hosts.Skill;
using Runtime.Bootstrapping;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<PlatformHostOptions>()
    .Bind(builder.Configuration.GetSection("AefPlatform"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(_ =>
{
    IReadOnlyDictionary<string, IBehavior> behaviors = new Dictionary<string, IBehavior>
    {
        ["SKL-EXE-0018"] = new DeliveryImplementationSkillBehavior(),
        ["SKL-EXE-0019"] = new DeliveryValidationSkillBehavior(),
        ["SKL-EXE-0020"] = new DeliveryDocumentationSkillBehavior(),
        ["SKL-EXE-0021"] = new ConversationIntakeAndRoutingBehavior(),
        ["SKL-EXE-0022"] = new ConversationHandoffAndFollowupBehavior(),
    };

    return RuntimeBootstrapper.Boot(
        new RuntimeConfiguration(PolicyResolver: new PermissiveActivationPolicyResolver()),
        behaviors);
});
builder.Services.AddSingleton(sp => new EngineeringExecutionPipeline(sp.GetRequiredService<RuntimeHandle>()));
builder.Services.AddHostedService<RuntimeStartupHostedService>();

var host = builder.Build();

await host.RunAsync();
```

- [ ] Make the change.
- [ ] `dotnet build AefPlatform.sln` succeeds.
- [ ] Manual boot check (Task 11 does the full one): `cd src/Host && dotnet run --no-build`, confirm `Runtime Architecture Loader: 24/24 artifacts registered.` and clean startup.
- [ ] Commit: `feat(host): wire Delivery Execution and Conversation Manager behaviors, expose EngineeringExecutionPipeline via DI`.

---

## Task 10: Live-Host end-to-end integration test

**Files:** Create `tests/Host.Tests/EngineeringExecutionPipelineHostIntegrationTests.cs`.

Mirrors `PlatformHostStartupTests`' `CreateBuilder` helper exactly, but wires the real behaviors dictionary (matching Task 9's `Program.cs`) instead of an empty one, then resolves `EngineeringExecutionPipeline` from the started host and executes a real feature-lifecycle request against the live, fully-booted Registry (all 24 manifest artifacts already registered by `RuntimeStartupHostedService`):

```csharp
using AefPlatform.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Runtime.Bootstrapping;
using Runtime.DomainContent.ConversationManagement;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.Execution;
using Runtime.Hosts.Skill;

namespace Host.Tests;

public class EngineeringExecutionPipelineHostIntegrationTests
{
    [Fact]
    public async Task Execute_FeatureRequest_AgainstTheRealBootedHost_DispatchesSuccessfully()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AefPlatform:PlatformName"] = "AEF Platform Runtime",
        });

        builder.Services
            .AddOptions<PlatformHostOptions>()
            .Bind(builder.Configuration.GetSection("AefPlatform"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton(_ =>
        {
            IReadOnlyDictionary<string, IBehavior> behaviors = new Dictionary<string, IBehavior>
            {
                ["SKL-EXE-0018"] = new DeliveryImplementationSkillBehavior(),
                ["SKL-EXE-0019"] = new DeliveryValidationSkillBehavior(),
                ["SKL-EXE-0020"] = new DeliveryDocumentationSkillBehavior(),
                ["SKL-EXE-0021"] = new ConversationIntakeAndRoutingBehavior(),
                ["SKL-EXE-0022"] = new ConversationHandoffAndFollowupBehavior(),
            };
            return RuntimeBootstrapper.Boot(new RuntimeConfiguration(PolicyResolver: new PermissiveActivationPolicyResolver()), behaviors);
        });
        builder.Services.AddSingleton(sp => new EngineeringExecutionPipeline(sp.GetRequiredService<RuntimeHandle>()));
        builder.Services.AddHostedService<RuntimeStartupHostedService>();

        using var host = builder.Build();
        await host.StartAsync();

        var pipeline = host.Services.GetRequiredService<EngineeringExecutionPipeline>();
        var result = pipeline.Execute(new EngineeringRequest("Please implement the new feature", "default-profile"));

        Assert.True(result.Success);
        Assert.Equal("WFL-DEL-0001", result.ExecutedWorkflowId);

        await host.StopAsync();
    }
}
```

- [ ] Write the test, confirm it fails only if wiring is wrong (should pass first try if Tasks 1-9 are correct — if it fails, the fix belongs in Task 9's `Program.cs` or an earlier task's file, not in this test).
- [ ] `dotnet test tests/Host.Tests/Host.Tests.csproj` — passes.
- [ ] Full suite green.
- [ ] Commit: `test(host): prove EngineeringExecutionPipeline dispatches correctly against the real booted Host`.

---

## Task 11: Final verification and documentation

**Files:** Modify `README.md`.

- [ ] `dotnet build AefPlatform.sln` — 0 warnings, 0 errors.
- [ ] `dotnet test AefPlatform.sln` — full suite green, record the total count.
- [ ] Manual boot: `cd src/Host && dotnet run --no-build`, confirm `Runtime Architecture Loader: 24/24 artifacts registered.` and no error-level logs.
- [ ] Update `README.md`'s `## Status` section (new entry at the top, above the P1 entry, preserving every existing entry) summarizing: the live-Activation blocker fix, the Conversation Manager routing/handoff layer, the Delivery Execution SkillHost adapters, the pipeline, and the explicit AGT-EXE-0012/0013/0014 out-of-scope gap.
- [ ] Commit: `docs: record P2 (Engineering Execution Engine) completion`.

## Definition of Done Evidence Map

| P2 DoD Item | Evidence |
|---|---|
| Engineering requests can be executed end-to-end | Task 10 live-Host integration test |
| Conversation lifecycle is operational | Task 3-5: `ConversationContext`, SKL-EXE-0021/0022 |
| Orchestrator routes requests successfully | Task 8 pipeline dispatch tests 1-2 |
| Registered agents execute through the runtime | Task 1 (blocker fix) + Task 8 tests 1-2 (Agent Activation actually succeeds) |
| Skills execute successfully | Task 6 adapter tests, Task 8 test 4 |
| Contracts execute successfully | Reused: each Delivery Behavior's typed Outcome (`IsEscalated`) surfaced into `ExecutionResult` |
| Workflows execute successfully | Task 8 tests 1-2 |
| Runtime execution context is propagated correctly | `EngineeringExecutionContext` (Task 7), threaded through `EngineeringExecutionPipeline.Execute` (Task 8) |
| Delivery Execution artifacts are integrated into the runtime | Task 2 (manifest), Task 6 (adapters), Task 9 (wired into live Host) |
| Structured execution results are returned | `ExecutionResult` (Task 7), returned by every `Execute` path (Task 8) |
| Automated tests pass | Task 11 full-suite run |
| Working tree clean / all work committed | Task 11 |
