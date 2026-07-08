# GA-2: AI Agent Integration Layer — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `Runtime.Agent` — the first stable, product-facing execution interface over the existing `EngineeringExecutionPipeline` — as a channel-agnostic library plus tests, with no live transport wired.

**Architecture:** A new class library `src/Runtime.Agent`, referencing only `Runtime` (never `Kernel` or `Runtime.Ai` — a compile-time guarantee enforced by an automated assembly-reference test, per `ADR-0025`). It exposes request/response contracts, a hand-authored capability catalog, an in-memory session store, and `AgentExecutionService` (`IAgentExecutionInterface`) as the single router into `EngineeringExecutionPipeline.Execute`. One additive, backward-compatible touch to `Runtime.Execution.ExecutionResult` (a new `Kind` field) lets `Runtime.Agent` distinguish `NeedsClarification` from `Failed`, which are provably ambiguous today.

**Tech Stack:** .NET 8, C# records/`sealed class`, xUnit, `Microsoft.Extensions.Hosting`/`DependencyInjection`/`Options` (already-referenced BCL/MS.Extensions packages only — no new NuGet dependency).

## Source Documents

- Design: `docs/superpowers/specs/2026-07-08-ga2-ai-agent-integration-layer-design.md` (Approved) — this plan implements it verbatim; do not re-derive or revisit its decisions.
- Architecture invariant: `../ai-engineering-framework/epics/decisions/0025-public-execution-interface-boundary.md` (ADR-0025, Accepted) — `Runtime.Agent` is the sole public execution boundary; `EngineeringExecutionPipeline` stays internal.

## Global Constraints

- Do NOT redesign architecture. Do NOT introduce transports, REST, CLI, or IDE integration (GA-3's job). Do NOT introduce asynchronous redesign — `AgentExecutionService.Execute` is synchronous, matching `EngineeringExecutionPipeline.Execute`'s own signature.
- Do NOT modify `Runtime.Ai`. Do NOT modify `EngineeringExecutionPipeline` beyond the one approved additive change (the `NeedsClarification` branch passing `Kind`).
- Do NOT introduce new NuGet packages. Every package used below (`Microsoft.NET.Test.Sdk` 17.8.0, `xunit`/`xunit.runner.visualstudio` 2.5.3, `coverlet.collector` 6.0.0) is already referenced elsewhere in this repo at these exact versions.
- `Runtime.Agent` references `Runtime` only — never `Kernel`, never `Runtime.Ai`, directly or transitively-visible-as-a-project-reference. Enforced by `ArchitectureBoundaryTests` (Task 2).
- No plugin framework, no multi-agent orchestration/autonomous planning, no persistent/long-term memory, no MCP, no live CLI/REST/IDE/Chat endpoint. `InMemorySessionStore` is process-lifetime only, never persisted.
- No new governed manifest entries (`PCT-`/`SCH-`/etc.) — `ExecutionRequest`/`ExecutionResponse`/etc. are implementation-only DTOs, the same precedent `ExecutionResult` itself set in GA-1.
- Full existing suite must stay green throughout (baseline captured in the Prerequisite step below) — re-run as the final gate (Task 9).

---

## Prerequisite: Merge the GA-2 design branch

The design spec and its `ExecutionResultKind` reservation note live on branch `docs/ga2-ai-agent-integration-layer-design` (2 commits: `b4315e3`, `eddc166`), not yet on `main`. Implementation must branch from `main` *after* this lands, so the design doc this plan implements is present at the implementation branch's root commit.

- [ ] **Step 1: Confirm the design branch is clean and push it**

Run: `git status` (expect: `nothing to commit, working tree clean`, current branch `docs/ga2-ai-agent-integration-layer-design`)
Run: `git push -u origin docs/ga2-ai-agent-integration-layer-design`

- [ ] **Step 2: Open and merge the docs PR**

```bash
gh pr create --title "docs(ga2): add AI Agent Integration Layer design spec" --body "$(cat <<'EOF'
## Summary
- Adds the GA-2 (AI Agent Integration Layer) design spec, approved and pending implementation.
- Reserves Completed/Failed in ExecutionResultKind and cites ADR-0025 (Public Execution Interface Boundary), recorded in the ai-engineering-framework repository.

No code changes. Documentation only.
EOF
)"
gh pr merge --auto --squash
```

- [ ] **Step 3: Update local `main`**

```bash
git checkout main
git pull
```

Verify: `docs/superpowers/specs/2026-07-08-ga2-ai-agent-integration-layer-design.md` exists on `main`.

---

## File Structure

```
src/Runtime/Execution/ExecutionResultKind.cs                        (new)
src/Runtime/Execution/ExecutionResult.cs                            (modify: add Kind field)
src/Runtime/Execution/EngineeringExecutionPipeline.cs               (modify: one call site sets Kind)

src/Runtime.Agent/Runtime.Agent.csproj                              (new project, net8.0, ProjectRef -> Runtime only)
src/Runtime.Agent/ExecutionStatus.cs
src/Runtime.Agent/ExecutionRequest.cs
src/Runtime.Agent/ExecutionError.cs
src/Runtime.Agent/ExecutionEvidence.cs
src/Runtime.Agent/ExecutionMetadata.cs
src/Runtime.Agent/ExecutionResponse.cs
src/Runtime.Agent/ExecutionSession.cs
src/Runtime.Agent/IExecutionProgressObserver.cs
src/Runtime.Agent/ExecutionProgressEvent.cs
src/Runtime.Agent/IAgentExecutionInterface.cs
src/Runtime.Agent/AgentExecutionService.cs
src/Runtime.Agent/Capabilities/CapabilityDescriptor.cs
src/Runtime.Agent/Capabilities/ICapabilityCatalog.cs
src/Runtime.Agent/Capabilities/EngineeringAgentCapabilityCatalog.cs
src/Runtime.Agent/Sessions/ISessionStore.cs
src/Runtime.Agent/Sessions/InMemorySessionStore.cs

src/Host/Program.cs                                                 (modify: register 3 new DI singletons)
src/Host/AefPlatform.Host.csproj                                    (modify: add ProjectRef -> Runtime.Agent)

tests/Runtime.Tests/Execution/ExecutionResultKindTests.cs           (new)
tests/Runtime.Tests/Execution/EngineeringExecutionPipelineTests.cs  (modify: 2 new facts)

tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj                (new test project)
tests/Runtime.Agent.Tests/ArchitectureBoundaryTests.cs
tests/Runtime.Agent.Tests/ExecutionContractsTests.cs
tests/Runtime.Agent.Tests/AgentExecutionServiceTests.cs
tests/Runtime.Agent.Tests/Capabilities/EngineeringAgentCapabilityCatalogTests.cs
tests/Runtime.Agent.Tests/Sessions/InMemorySessionStoreTests.cs

tests/Host.Tests/GA2AgentExecutionHostTests.cs                      (new)

AefPlatform.sln                                                     (modify: add Runtime.Agent + Runtime.Agent.Tests projects)
README.md                                                           (modify: new Status paragraph)
docs/superpowers/specs/2026-07-08-ga2-ai-agent-integration-layer-design.md (modify: Status line)
```

---

## Task 1: `Runtime.Execution` contract enhancement — `ExecutionResultKind`

**Files:**
- Create: `src/Runtime/Execution/ExecutionResultKind.cs`
- Modify: `src/Runtime/Execution/ExecutionResult.cs`
- Modify: `src/Runtime/Execution/EngineeringExecutionPipeline.cs`
- Test: `tests/Runtime.Tests/Execution/ExecutionResultKindTests.cs`, `tests/Runtime.Tests/Execution/EngineeringExecutionPipelineTests.cs`

**Interfaces:**
- Produces: `Runtime.Execution.ExecutionResultKind` (`Unspecified = 0`, `Completed = 1`, `NeedsClarification = 2`, `Failed = 3`).
- Produces: `ExecutionResult.Kind` — new trailing optional `ExecutionResultKind` parameter, default `Unspecified`. Every later task's `AgentExecutionService` reads this field.

- [ ] **Step 1: Write the failing test**

`tests/Runtime.Tests/Execution/ExecutionResultKindTests.cs`:
```csharp
namespace Runtime.Tests.Execution;

public class ExecutionResultKindTests
{
    [Fact]
    public void DefaultConstructorLeavesKindUnspecified()
    {
        var result = new ExecutionResult(true, "WFL-DEL-0001", null, Array.Empty<string>(), null, TimeSpan.Zero, null, null);

        Assert.Equal(Runtime.Execution.ExecutionResultKind.Unspecified, result.Kind);
    }

    [Fact]
    public void KindCanBeSetExplicitlyViaNamedArgument()
    {
        var result = new ExecutionResult(false, null, null, Array.Empty<string>(), null, TimeSpan.Zero, null, null,
            Kind: Runtime.Execution.ExecutionResultKind.NeedsClarification);

        Assert.Equal(Runtime.Execution.ExecutionResultKind.NeedsClarification, result.Kind);
    }
}
```

Also append two new facts to the existing `tests/Runtime.Tests/Execution/EngineeringExecutionPipelineTests.cs` — insert immediately after `Execute_UnroutableRequest_ReturnsFailureWithClarificationDiagnostics` (which ends `}` on line 86):

```csharp
    [Fact]
    public void Execute_UnroutableRequest_SetsKindToNeedsClarification()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("What time is it?", "default-profile"));

        Assert.Equal(ExecutionResultKind.NeedsClarification, result.Kind);
    }
```

...and insert immediately after `Execute_WorkflowNotRegistered_ReturnsFailureDiagnostics` (which ends `}` on line 125):

```csharp
    [Fact]
    public void Execute_WorkflowNotRegistered_KindStaysUnspecified()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("implement a feature", "default-profile"));

        Assert.Equal(ExecutionResultKind.Unspecified, result.Kind);
    }
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run: `dotnet test tests/Runtime.Tests/Runtime.Tests.csproj --filter "FullyQualifiedName~ExecutionResultKind|FullyQualifiedName~Execute_UnroutableRequest_SetsKindToNeedsClarification|FullyQualifiedName~Execute_WorkflowNotRegistered_KindStaysUnspecified"`
Expected: build error — `ExecutionResultKind` does not exist and `ExecutionResult` has no `Kind` member/constructor overload.

- [ ] **Step 3: Add the enum**

`src/Runtime/Execution/ExecutionResultKind.cs`:
```csharp
namespace Runtime.Execution;

public enum ExecutionResultKind
{
    Unspecified = 0,
    Completed = 1,
    NeedsClarification = 2,
    Failed = 3,
}
```

- [ ] **Step 4: Add the trailing field to `ExecutionResult`**

`src/Runtime/Execution/ExecutionResult.cs` — replace the whole file:
```csharp
namespace Runtime.Execution;

// The structured execution result P2 requires. No governed PCT-/SCH-
// schema exists for this generic shape anywhere in the architecture - this
// is a genuine, implementation-only DTO.
public sealed record ExecutionResult(
    bool Success,
    string? ExecutedWorkflowId,
    string? ExecutedAgentId,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string>? ValidationResults,
    TimeSpan Duration,
    string? PresentedResult,
    string? SuggestedNextStep,
    string? RepositoryAnalyzed = null,
    IReadOnlyList<string>? FilesChanged = null,
    bool? TestsExecuted = null,
    ExecutionResultKind Kind = ExecutionResultKind.Unspecified);
```

- [ ] **Step 5: Set `Kind` on the one approved call site**

`src/Runtime/Execution/EngineeringExecutionPipeline.cs` — in `Execute`, replace:
```csharp
            var handoff = Handoff(routingResult, dispatchOutcome: null);
            stopwatch.Stop();
            return new ExecutionResult(
                Success: false, null, null, executionContext.Diagnostics, null, stopwatch.Elapsed,
                handoff.PresentedResult, handoff.SuggestedNextStep);
```
with:
```csharp
            var handoff = Handoff(routingResult, dispatchOutcome: null);
            stopwatch.Stop();
            return new ExecutionResult(
                Success: false, null, null, executionContext.Diagnostics, null, stopwatch.Elapsed,
                handoff.PresentedResult, handoff.SuggestedNextStep, Kind: ExecutionResultKind.NeedsClarification);
```

No other call site in this file changes — every other `new ExecutionResult(...)` construction (roughly a dozen, across every `From*` method and `FromOrchestration`) keeps defaulting `Kind` to `Unspecified`.

- [ ] **Step 6: Run the tests to confirm they pass**

Run: `dotnet test tests/Runtime.Tests/Runtime.Tests.csproj`
Expected: all pass, including the full pre-existing `Runtime.Tests` suite (no regression from the additive field).

- [ ] **Step 7: Commit**

```bash
git checkout -b feature/ga2-ai-agent-integration-layer
git add src/Runtime/Execution/ExecutionResultKind.cs src/Runtime/Execution/ExecutionResult.cs src/Runtime/Execution/EngineeringExecutionPipeline.cs tests/Runtime.Tests/Execution/ExecutionResultKindTests.cs tests/Runtime.Tests/Execution/EngineeringExecutionPipelineTests.cs
git commit -m "feat(runtime): add ExecutionResultKind, distinguishing NeedsClarification from Failed"
```

---

## Task 2: `Runtime.Agent` project creation

**Files:**
- Create: `src/Runtime.Agent/Runtime.Agent.csproj`
- Create: `src/Runtime.Agent/ExecutionStatus.cs`
- Modify: `AefPlatform.sln`
- Test: `tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj`, `tests/Runtime.Agent.Tests/ArchitectureBoundaryTests.cs`

**Interfaces:**
- Produces: `Runtime.Agent.ExecutionStatus` (`Completed`, `Failed`, `NeedsClarification`) — the public status enum every later task's contracts and `AgentExecutionService` return.
- Produces: the structural guarantee (ADR-0025) that the `Runtime.Agent` assembly never references `Kernel` or `Runtime.Ai`, verified by an automated test rather than only a `.csproj` convention.

- [ ] **Step 1: Create the project file**

`src/Runtime.Agent/Runtime.Agent.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Runtime\Runtime.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add the project to the solution**

Run: `dotnet sln AefPlatform.sln add src/Runtime.Agent/Runtime.Agent.csproj`

- [ ] **Step 3: Write `ExecutionStatus`**

`src/Runtime.Agent/ExecutionStatus.cs`:
```csharp
namespace Runtime.Agent;

public enum ExecutionStatus
{
    Completed,
    Failed,
    NeedsClarification,
}
```

- [ ] **Step 4: Create the test project**

`tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Runtime.Agent\Runtime.Agent.csproj" />
    <ProjectReference Include="..\..\src\Runtime\Runtime.csproj" />
    <ProjectReference Include="..\..\src\Kernel\Kernel.csproj" />
  </ItemGroup>

</Project>
```

Note: the *test* project references `Runtime`/`Kernel` directly because later tasks' tests boot a real `RuntimeHandle` (the same pattern `Runtime.Tests` already uses) — only the `Runtime.Agent` *product* assembly itself is constrained by ADR-0025.

Run: `dotnet sln AefPlatform.sln add tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj`

- [ ] **Step 5: Write the failing architecture-boundary test**

`tests/Runtime.Agent.Tests/ArchitectureBoundaryTests.cs`:
```csharp
namespace Runtime.Agent.Tests;

// Enforces ADR-0025 (../ai-engineering-framework/epics/decisions/
// 0025-public-execution-interface-boundary.md) as an automated guarantee:
// Runtime.Agent must never acquire a reference to Kernel or Runtime.Ai. A
// failure here means a future change gave Runtime.Agent a project
// reference this architecture forbids - caught here, not at review time.
public class ArchitectureBoundaryTests
{
    private static IReadOnlyList<string?> RuntimeAgentReferencedAssemblyNames() =>
        typeof(ExecutionStatus).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

    [Fact]
    public void RuntimeAgentAssembly_DoesNotReferenceKernel()
    {
        Assert.DoesNotContain("Kernel", RuntimeAgentReferencedAssemblyNames());
    }

    [Fact]
    public void RuntimeAgentAssembly_DoesNotReferenceRuntimeAi()
    {
        Assert.DoesNotContain("Runtime.Ai", RuntimeAgentReferencedAssemblyNames());
    }

    [Fact]
    public void RuntimeAgentAssembly_ReferencesRuntime()
    {
        Assert.Contains("Runtime", RuntimeAgentReferencedAssemblyNames());
    }
}
```

- [ ] **Step 6: Run the tests, confirm they pass**

Run: `dotnet test tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj`
Expected: 3 pass (this is a scaffolding task — the project and `ExecutionStatus` exist by Step 3, so there is no red/green cycle beyond "does the assembly reference graph already satisfy ADR-0025," which it does since the `.csproj` only references `Runtime`).

- [ ] **Step 7: Commit**

```bash
git add src/Runtime.Agent tests/Runtime.Agent.Tests AefPlatform.sln
git commit -m "feat(runtime.agent): scaffold Runtime.Agent project with an automated ADR-0025 boundary guard"
```

---

## Task 3: Public execution contracts

**Files:**
- Create: `src/Runtime.Agent/ExecutionRequest.cs`
- Create: `src/Runtime.Agent/ExecutionError.cs`
- Create: `src/Runtime.Agent/ExecutionEvidence.cs`
- Create: `src/Runtime.Agent/ExecutionMetadata.cs`
- Create: `src/Runtime.Agent/ExecutionResponse.cs`
- Create: `src/Runtime.Agent/ExecutionSession.cs`
- Create: `src/Runtime.Agent/IExecutionProgressObserver.cs`
- Create: `src/Runtime.Agent/ExecutionProgressEvent.cs`
- Test: `tests/Runtime.Agent.Tests/ExecutionContractsTests.cs`

**Interfaces:**
- Consumes: `ExecutionStatus` (Task 2).
- Produces: `ExecutionRequest(string RequestId, string? SessionId, string UserRequest, string ProjectProfileId, IReadOnlyDictionary<string,object>? Payload, IExecutionProgressObserver? ProgressObserver = null)`.
- Produces: `ExecutionResponse(string RequestId, string? SessionId, ExecutionStatus Status, ExecutionError? Error, string? PresentedResult, string? SuggestedNextStep, ExecutionEvidence Evidence, ExecutionMetadata Metadata)`.
- Produces: `ExecutionError(string Reason, IReadOnlyList<string> Diagnostics)`.
- Produces: `ExecutionEvidence(IReadOnlyList<string> Diagnostics, IReadOnlyList<string>? ValidationResults, string? RepositoryAnalyzed, IReadOnlyList<string>? FilesChanged, bool? TestsExecuted)`.
- Produces: `ExecutionMetadata(string? ExecutedCapabilityId, string? ExecutedAgentId, TimeSpan Duration, DateTimeOffset StartedAt, DateTimeOffset CompletedAt)`.
- Produces: `ExecutionSession(string SessionId, DateTimeOffset CreatedAt, DateTimeOffset LastActivityAt, string? LastProjectProfileId, IReadOnlyDictionary<string,object>? LastPayload, int TurnCount)` — Task 5's `ISessionStore` and Task 6's `AgentExecutionService` both depend on this exact shape.
- Produces: `IExecutionProgressObserver.OnProgress(ExecutionProgressEvent)` and `ExecutionProgressEvent(string RequestId, string Stage, string Message, DateTimeOffset Timestamp)` — Task 6 fires exactly two of these per `Execute` call.

- [ ] **Step 1: Write the failing test**

`tests/Runtime.Agent.Tests/ExecutionContractsTests.cs`:
```csharp
namespace Runtime.Agent.Tests;

public class ExecutionContractsTests
{
    [Fact]
    public void ExecutionRequest_PayloadAndProgressObserverDefaultToNull()
    {
        var request = new ExecutionRequest("req-1", null, "do the thing", "default-profile", null);

        Assert.Null(request.SessionId);
        Assert.Null(request.Payload);
        Assert.Null(request.ProgressObserver);
    }

    [Fact]
    public void ExecutionResponse_CarriesEvidenceAndMetadataVerbatim()
    {
        var evidence = new ExecutionEvidence(new[] { "diag" }, null, null, null, null);
        var metadata = new ExecutionMetadata("WFL-DEL-0001", "AGT-EXE-0002", TimeSpan.FromSeconds(1), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var response = new ExecutionResponse("req-1", "session-1", ExecutionStatus.Completed, null, "done", "no further action", evidence, metadata);

        Assert.Equal(ExecutionStatus.Completed, response.Status);
        Assert.Same(evidence, response.Evidence);
        Assert.Same(metadata, response.Metadata);
    }

    [Fact]
    public void ExecutionSession_TurnCountAndPayloadAreExplicit()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new ExecutionSession("session-1", now, now, "default-profile", null, 0);

        Assert.Equal(0, session.TurnCount);
        Assert.Null(session.LastPayload);
    }

    [Fact]
    public void ExecutionProgressEvent_CarriesStageAndTimestamp()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var progressEvent = new ExecutionProgressEvent("req-1", "Routing", "Routing request.", timestamp);

        Assert.Equal("Routing", progressEvent.Stage);
        Assert.Equal(timestamp, progressEvent.Timestamp);
    }
}
```

- [ ] **Step 2: Run the test to confirm it fails**

Run: `dotnet test tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj --filter "FullyQualifiedName~ExecutionContractsTests"`
Expected: build error — none of `ExecutionRequest`/`ExecutionResponse`/`ExecutionError`/`ExecutionEvidence`/`ExecutionMetadata`/`ExecutionSession`/`ExecutionProgressEvent` exist yet.

- [ ] **Step 3: Write the contracts**

`src/Runtime.Agent/ExecutionRequest.cs`:
```csharp
namespace Runtime.Agent;

// The public, channel-agnostic execution request every future GA-3 channel
// (CLI/REST/IDE/Chat) will construct. ProjectProfileId is a required
// string, matching Runtime.Execution.EngineeringRequest's own shape; an
// empty string on a request carrying a SessionId means "inherit the
// session's last value" (AgentExecutionService, Task 6) - there is no
// separate nullable variant of this field to keep this contract a direct,
// honest mirror of EngineeringRequest.
public sealed record ExecutionRequest(
    string RequestId,
    string? SessionId,
    string UserRequest,
    string ProjectProfileId,
    IReadOnlyDictionary<string, object>? Payload,
    IExecutionProgressObserver? ProgressObserver = null);
```

`src/Runtime.Agent/ExecutionError.cs`:
```csharp
namespace Runtime.Agent;

// Populated whenever ExecutionResponse.Status != ExecutionStatus.Completed.
public sealed record ExecutionError(string Reason, IReadOnlyList<string> Diagnostics);
```

`src/Runtime.Agent/ExecutionEvidence.cs`:
```csharp
namespace Runtime.Agent;

// A direct, lossless remap of Runtime.Execution.ExecutionResult's own
// evidentiary fields - deliberately not reaching into AiExecutionService's
// IEvidenceSink, since EvidenceRecord carries no request/session
// correlation id today (see the GA-2 design spec's "Known non-goal").
public sealed record ExecutionEvidence(
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string>? ValidationResults,
    string? RepositoryAnalyzed,
    IReadOnlyList<string>? FilesChanged,
    bool? TestsExecuted);
```

`src/Runtime.Agent/ExecutionMetadata.cs`:
```csharp
namespace Runtime.Agent;

public sealed record ExecutionMetadata(
    string? ExecutedCapabilityId,
    string? ExecutedAgentId,
    TimeSpan Duration,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
```

`src/Runtime.Agent/ExecutionResponse.cs`:
```csharp
namespace Runtime.Agent;

public sealed record ExecutionResponse(
    string RequestId,
    string? SessionId,
    ExecutionStatus Status,
    ExecutionError? Error,
    string? PresentedResult,
    string? SuggestedNextStep,
    ExecutionEvidence Evidence,
    ExecutionMetadata Metadata);
```

`src/Runtime.Agent/ExecutionSession.cs`:
```csharp
namespace Runtime.Agent;

// Pure caller convenience, process-lifetime only (InMemorySessionStore,
// Task 5). Never influences routing or dispatch - those happen exclusively
// inside EngineeringExecutionPipeline, per request, deterministically.
public sealed record ExecutionSession(
    string SessionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    string? LastProjectProfileId,
    IReadOnlyDictionary<string, object>? LastPayload,
    int TurnCount);
```

`src/Runtime.Agent/IExecutionProgressObserver.cs`:
```csharp
namespace Runtime.Agent;

public interface IExecutionProgressObserver
{
    void OnProgress(ExecutionProgressEvent progressEvent);
}
```

`src/Runtime.Agent/ExecutionProgressEvent.cs`:
```csharp
namespace Runtime.Agent;

public sealed record ExecutionProgressEvent(string RequestId, string Stage, string Message, DateTimeOffset Timestamp);
```

- [ ] **Step 4: Run the test to confirm it passes**

Run: `dotnet test tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj --filter "FullyQualifiedName~ExecutionContractsTests"`
Expected: 4 pass.

- [ ] **Step 5: Commit**

```bash
git add src/Runtime.Agent/ExecutionRequest.cs src/Runtime.Agent/ExecutionError.cs src/Runtime.Agent/ExecutionEvidence.cs src/Runtime.Agent/ExecutionMetadata.cs src/Runtime.Agent/ExecutionResponse.cs src/Runtime.Agent/ExecutionSession.cs src/Runtime.Agent/IExecutionProgressObserver.cs src/Runtime.Agent/ExecutionProgressEvent.cs tests/Runtime.Agent.Tests/ExecutionContractsTests.cs
git commit -m "feat(runtime.agent): add public execution request/response/session contracts"
```

---

## Task 4: Capability catalog

**Files:**
- Create: `src/Runtime.Agent/Capabilities/CapabilityDescriptor.cs`
- Create: `src/Runtime.Agent/Capabilities/ICapabilityCatalog.cs`
- Create: `src/Runtime.Agent/Capabilities/EngineeringAgentCapabilityCatalog.cs`
- Test: `tests/Runtime.Agent.Tests/Capabilities/EngineeringAgentCapabilityCatalogTests.cs`

**Interfaces:**
- Produces: `CapabilityDescriptor(string CapabilityId, string DisplayName, string Description, IReadOnlyList<string> RequiredPayloadKeys, string ExecutionMode)`.
- Produces: `ICapabilityCatalog.Discover() : IReadOnlyList<CapabilityDescriptor>` — consumed by `IAgentExecutionInterface.DiscoverCapabilities()` (Task 6) and DI (Task 7).

- [ ] **Step 1: Write the failing test**

`tests/Runtime.Agent.Tests/Capabilities/EngineeringAgentCapabilityCatalogTests.cs`:
```csharp
using Runtime.Agent.Capabilities;

namespace Runtime.Agent.Tests.Capabilities;

public class EngineeringAgentCapabilityCatalogTests
{
    private static readonly string[] KnownDispatchableIds =
    {
        "WFL-DEL-0001", "WFL-DEL-0004", "WFL-DEL-0005", "WFL-DEL-0006",
        "WFL-DEL-0007", "WFL-DEL-0008", "SKL-EXE-0018", "REPO-VALIDATION",
    };

    [Fact]
    public void Discover_ReturnsExactlyOneDescriptorPerKnownDispatchableId()
    {
        var catalog = new EngineeringAgentCapabilityCatalog();

        var descriptors = catalog.Discover();

        foreach (var id in KnownDispatchableIds)
        {
            var matches = descriptors.Where(d => d.CapabilityId == id).ToList();
            Assert.Single(matches);
            Assert.False(string.IsNullOrWhiteSpace(matches[0].Description));
            Assert.Equal("Synchronous", matches[0].ExecutionMode);
        }
        Assert.Equal(KnownDispatchableIds.Length, descriptors.Count);
    }

    [Fact]
    public void Discover_UnknownIdHasNoDescriptor()
    {
        var catalog = new EngineeringAgentCapabilityCatalog();

        var descriptors = catalog.Discover();

        Assert.DoesNotContain(descriptors, d => d.CapabilityId == "NOT-A-REAL-CAPABILITY");
    }
}
```

- [ ] **Step 2: Run the test to confirm it fails**

Run: `dotnet test tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj --filter "FullyQualifiedName~EngineeringAgentCapabilityCatalogTests"`
Expected: build error — `Runtime.Agent.Capabilities` namespace does not exist yet.

- [ ] **Step 3: Write the catalog**

`src/Runtime.Agent/Capabilities/CapabilityDescriptor.cs`:
```csharp
namespace Runtime.Agent.Capabilities;

public sealed record CapabilityDescriptor(
    string CapabilityId,
    string DisplayName,
    string Description,
    IReadOnlyList<string> RequiredPayloadKeys,
    string ExecutionMode);
```

`src/Runtime.Agent/Capabilities/ICapabilityCatalog.cs`:
```csharp
namespace Runtime.Agent.Capabilities;

public interface ICapabilityCatalog
{
    IReadOnlyList<CapabilityDescriptor> Discover();
}
```

`src/Runtime.Agent/Capabilities/EngineeringAgentCapabilityCatalog.cs`:
```csharp
namespace Runtime.Agent.Capabilities;

// Hand-authored, one entry per capability id
// EngineeringExecutionPipeline.Dispatch already switches on
// (src/Runtime/Execution/EngineeringExecutionPipeline.cs). Kept in sync
// manually - the same precedent EngineeringArchitectureManifest already
// sets for its own pipeline-coupled table - since there is no richer
// metadata anywhere in the Runtime to reflect this from instead.
// ExecutionMode is always "Synchronous" today: the field is the extension
// point the GA-2 brief asks for ("supported execution modes"), not because
// multiple modes exist yet.
public sealed class EngineeringAgentCapabilityCatalog : ICapabilityCatalog
{
    private static readonly IReadOnlyList<CapabilityDescriptor> Capabilities = new[]
    {
        new CapabilityDescriptor(
            "WFL-DEL-0001", "Start Feature Lifecycle",
            "Starts the Feature Lifecycle Orchestrator for a new feature request.",
            Array.Empty<string>(), "Synchronous"),
        new CapabilityDescriptor(
            "WFL-DEL-0004", "Start Epic Lifecycle",
            "Starts the Epic Lifecycle Orchestrator for a new epic request.",
            Array.Empty<string>(), "Synchronous"),
        new CapabilityDescriptor(
            "WFL-DEL-0005", "Implementation Validation",
            "Runs specification-conformance and architecture-standards validation and synthesizes a merge-readiness signal.",
            new[] { "SpecificationConformanceValidationRequest", "ArchitectureStandardsConformanceReviewRequest" }, "Synchronous"),
        new CapabilityDescriptor(
            "WFL-DEL-0006", "Engineering Planning",
            "Runs Discovery, Specification Drafting, and Readiness Classification against a repository to produce a Business-Approved Specification.",
            new[] { "RepositoryName", "RepositoryRoot" }, "Synchronous"),
        new CapabilityDescriptor(
            "WFL-DEL-0007", "Specification Review",
            "Runs an independent review of a drafted specification.",
            new[] { "SpecificationReviewRequest" }, "Synchronous"),
        new CapabilityDescriptor(
            "WFL-DEL-0008", "Repository Intelligence",
            "Runs Repository & Architecture Understanding, Code Intelligence, optional Diagnostics, and Recommendation Synthesis against a repository.",
            new[] { "RepositoryName", "RepositoryRoot" }, "Synchronous"),
        new CapabilityDescriptor(
            "SKL-EXE-0018", "Delivery Implementation",
            "Executes a governed Delivery Execution implementation request against a repository.",
            new[] { "ImplementationRequest", "RepositoryRoot" }, "Synchronous"),
        new CapabilityDescriptor(
            "REPO-VALIDATION", "Repository Validation",
            "Runs the detected build/test command for a repository and records a verification outcome.",
            new[] { "RepositoryName", "RepositoryRoot" }, "Synchronous"),
    };

    public IReadOnlyList<CapabilityDescriptor> Discover() => Capabilities;
}
```

- [ ] **Step 4: Run the test to confirm it passes**

Run: `dotnet test tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj --filter "FullyQualifiedName~EngineeringAgentCapabilityCatalogTests"`
Expected: 2 pass.

- [ ] **Step 5: Commit**

```bash
git add src/Runtime.Agent/Capabilities tests/Runtime.Agent.Tests/Capabilities
git commit -m "feat(runtime.agent): add EngineeringAgentCapabilityCatalog"
```

---

## Task 5: Session store

**Files:**
- Create: `src/Runtime.Agent/Sessions/ISessionStore.cs`
- Create: `src/Runtime.Agent/Sessions/InMemorySessionStore.cs`
- Test: `tests/Runtime.Agent.Tests/Sessions/InMemorySessionStoreTests.cs`

**Interfaces:**
- Consumes: `ExecutionSession` (Task 3).
- Produces: `ISessionStore.Create(string?) : ExecutionSession`, `.Get(string) : ExecutionSession?`, `.Touch(string, string?, IReadOnlyDictionary<string,object>?) : ExecutionSession` — `AgentExecutionService` (Task 6) depends on all three signatures exactly.

- [ ] **Step 1: Write the failing test**

`tests/Runtime.Agent.Tests/Sessions/InMemorySessionStoreTests.cs`:
```csharp
using Runtime.Agent.Sessions;

namespace Runtime.Agent.Tests.Sessions;

public class InMemorySessionStoreTests
{
    [Fact]
    public void Create_ProducesEmptySessionState()
    {
        var store = new InMemorySessionStore();

        var session = store.Create("default-profile");

        Assert.Equal("default-profile", session.LastProjectProfileId);
        Assert.Null(session.LastPayload);
        Assert.Equal(0, session.TurnCount);
        Assert.Equal(session.CreatedAt, session.LastActivityAt);
    }

    [Fact]
    public void Get_UnknownSessionId_ReturnsNull()
    {
        var store = new InMemorySessionStore();

        Assert.Null(store.Get("does-not-exist"));
    }

    [Fact]
    public void Touch_UpdatesLastActivityTurnCountAndMergesPayload()
    {
        var store = new InMemorySessionStore();
        var created = store.Create("default-profile");
        var payload = new Dictionary<string, object> { ["Goal"] = "ship it" };

        var touched = store.Touch(created.SessionId, "default-profile", payload);

        Assert.Equal(1, touched.TurnCount);
        Assert.True(touched.LastActivityAt >= created.LastActivityAt);
        Assert.Same(payload, touched.LastPayload);
        Assert.Equal(touched, store.Get(created.SessionId));
    }

    [Fact]
    public void Touch_OmittedPayload_RetainsPriorPayload()
    {
        var store = new InMemorySessionStore();
        var created = store.Create("default-profile");
        var payload = new Dictionary<string, object> { ["Goal"] = "ship it" };
        store.Touch(created.SessionId, "default-profile", payload);

        var touchedAgain = store.Touch(created.SessionId, "default-profile", null);

        Assert.Same(payload, touchedAgain.LastPayload);
        Assert.Equal(2, touchedAgain.TurnCount);
    }

    [Fact]
    public void Touch_UnknownSessionId_Throws()
    {
        var store = new InMemorySessionStore();

        Assert.Throws<KeyNotFoundException>(() => store.Touch("does-not-exist", null, null));
    }
}
```

- [ ] **Step 2: Run the test to confirm it fails**

Run: `dotnet test tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj --filter "FullyQualifiedName~InMemorySessionStoreTests"`
Expected: build error — `Runtime.Agent.Sessions` namespace does not exist yet.

- [ ] **Step 3: Write the session store**

`src/Runtime.Agent/Sessions/ISessionStore.cs`:
```csharp
namespace Runtime.Agent.Sessions;

public interface ISessionStore
{
    ExecutionSession Create(string? projectProfileId);
    ExecutionSession? Get(string sessionId);
    ExecutionSession Touch(string sessionId, string? projectProfileId, IReadOnlyDictionary<string, object>? payload);
}
```

`src/Runtime.Agent/Sessions/InMemorySessionStore.cs`:
```csharp
using System.Collections.Concurrent;

namespace Runtime.Agent.Sessions;

// Process-lifetime only, never persisted to disk or any store - satisfies
// "no persistent memory / no long-term knowledge storage" literally, while
// still delivering conversation/session support. Session state is pure
// caller convenience: it never influences routing or dispatch.
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, ExecutionSession> _sessions = new();

    public ExecutionSession Create(string? projectProfileId)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new ExecutionSession(Guid.NewGuid().ToString("N"), now, now, projectProfileId, null, TurnCount: 0);
        _sessions[session.SessionId] = session;
        return session;
    }

    public ExecutionSession? Get(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) ? session : null;

    public ExecutionSession Touch(string sessionId, string? projectProfileId, IReadOnlyDictionary<string, object>? payload)
    {
        if (!_sessions.TryGetValue(sessionId, out var existing))
        {
            throw new KeyNotFoundException($"No session exists for id '{sessionId}'.");
        }

        var updated = existing with
        {
            LastActivityAt = DateTimeOffset.UtcNow,
            LastProjectProfileId = projectProfileId ?? existing.LastProjectProfileId,
            LastPayload = payload ?? existing.LastPayload,
            TurnCount = existing.TurnCount + 1,
        };
        _sessions[sessionId] = updated;
        return updated;
    }
}
```

- [ ] **Step 4: Run the test to confirm it passes**

Run: `dotnet test tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj --filter "FullyQualifiedName~InMemorySessionStoreTests"`
Expected: 5 pass.

- [ ] **Step 5: Commit**

```bash
git add src/Runtime.Agent/Sessions tests/Runtime.Agent.Tests/Sessions
git commit -m "feat(runtime.agent): add InMemorySessionStore"
```

---

## Task 6: `AgentExecutionService` — the router

**Files:**
- Create: `src/Runtime.Agent/IAgentExecutionInterface.cs`
- Create: `src/Runtime.Agent/AgentExecutionService.cs`
- Test: `tests/Runtime.Agent.Tests/AgentExecutionServiceTests.cs`

**Interfaces:**
- Consumes: `EngineeringExecutionPipeline.Execute(EngineeringRequest) : ExecutionResult` (unmodified, `Runtime.Execution`), `ICapabilityCatalog.Discover()` (Task 4), `ISessionStore.Create/Get/Touch` (Task 5), all of Task 3's contracts.
- Produces: `IAgentExecutionInterface { Execute(ExecutionRequest) : ExecutionResponse; DiscoverCapabilities() : IReadOnlyList<CapabilityDescriptor>; StartSession(string?) : ExecutionSession }` and `AgentExecutionService : IAgentExecutionInterface` — Task 7's DI wiring depends on this exact constructor: `AgentExecutionService(EngineeringExecutionPipeline, ICapabilityCatalog, ISessionStore)`.

- [ ] **Step 1: Write the failing tests**

`tests/Runtime.Agent.Tests/AgentExecutionServiceTests.cs`:
```csharp
using Kernel.Governance;
using Kernel.Registry;
using Runtime.Agent.Capabilities;
using Runtime.Agent.Sessions;
using Runtime.Bootstrapping;
using Runtime.Context;
using Runtime.DomainContent.ConversationManagement;
using Runtime.Execution;
using Runtime.Hosts.Skill;

namespace Runtime.Agent.Tests;

public class AgentExecutionServiceTests
{
    private static RuntimeHandle BootWithBehaviors(IReadOnlyDictionary<string, IBehavior> behaviors) =>
        RuntimeBootstrapper.Boot(new RuntimeConfiguration(PolicyResolver: new AllowAllPolicyResolver()), behaviors);

    private static IReadOnlyDictionary<string, IBehavior> ConversationBehaviors(IBehavior? routingBehavior = null) =>
        new Dictionary<string, IBehavior>
        {
            ["SKL-EXE-0021"] = routingBehavior ?? new ConversationIntakeAndRoutingBehavior(),
            ["SKL-EXE-0022"] = new ConversationHandoffAndFollowupBehavior(),
        };

    private static void RegisterFeatureCapabilities(RuntimeHandle runtime)
    {
        runtime.Kernel.Registry.Register(new DefinitionSubmission("ORC-EXE-0001", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("WFL-DEL-0001", "Delivery"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("AGT-EXE-0002", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
    }

    private static AgentExecutionService NewService(RuntimeHandle runtime, ISessionStore? sessionStore = null) =>
        new(new EngineeringExecutionPipeline(runtime), new EngineeringAgentCapabilityCatalog(), sessionStore ?? new InMemorySessionStore());

    [Fact]
    public void Execute_CompletedPath_MapsCapabilityAgentDiagnosticsAndPresentedResult()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        RegisterFeatureCapabilities(runtime);
        var service = NewService(runtime);

        var response = service.Execute(new ExecutionRequest("req-1", null, "Please implement the new feature", "default-profile", null));

        Assert.Equal(ExecutionStatus.Completed, response.Status);
        Assert.Null(response.Error);
        Assert.Equal("WFL-DEL-0001", response.Metadata.ExecutedCapabilityId);
    }

    [Fact]
    public void Execute_UnroutableRequest_ReturnsNeedsClarificationStatus()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var service = NewService(runtime);

        var response = service.Execute(new ExecutionRequest("req-1", null, "What time is it?", "default-profile", null));

        Assert.Equal(ExecutionStatus.NeedsClarification, response.Status);
        Assert.NotNull(response.Error);
    }

    [Fact]
    public void Execute_DispatchFailure_ReturnsFailedStatus_DistinctFromNeedsClarification()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var service = NewService(runtime);

        var response = service.Execute(new ExecutionRequest("req-1", null, "implement a feature", "default-profile", null));

        Assert.Equal(ExecutionStatus.Failed, response.Status);
        Assert.NotNull(response.Error);
    }

    [Fact]
    public void Execute_SessionRoundTrip_SecondCallOmittingProjectProfileIdInheritsPriorTurnValue()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        RegisterFeatureCapabilities(runtime);
        var service = NewService(runtime);

        var session = service.StartSession("default-profile");
        var first = service.Execute(new ExecutionRequest("req-1", session.SessionId, "Please implement the new feature", "default-profile", null));
        Assert.Equal(ExecutionStatus.Completed, first.Status);
        Assert.Equal(session.SessionId, first.SessionId);

        var second = service.Execute(new ExecutionRequest("req-2", session.SessionId, "Please implement the new feature", "", null));

        Assert.Equal(ExecutionStatus.Completed, second.Status);
        Assert.Equal(session.SessionId, second.SessionId);
    }

    [Fact]
    public void Execute_UnknownSessionId_ReturnsErrorWithoutInvokingPipeline()
    {
        var countingBehavior = new CountingRoutingBehavior();
        var runtime = BootWithBehaviors(ConversationBehaviors(countingBehavior));
        var service = NewService(runtime);

        var response = service.Execute(new ExecutionRequest("req-1", "unknown-session-id", "Please implement the new feature", "default-profile", null));

        Assert.Equal(ExecutionStatus.Failed, response.Status);
        Assert.NotNull(response.Error);
        Assert.Equal(0, countingBehavior.InvocationCount);
    }

    [Fact]
    public void Execute_WithProgressObserver_ReceivesExactlyRoutingAndTerminalEvents()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        RegisterFeatureCapabilities(runtime);
        var service = NewService(runtime);
        var observer = new RecordingProgressObserver();

        var response = service.Execute(new ExecutionRequest("req-1", null, "Please implement the new feature", "default-profile", null, observer));

        Assert.Equal(2, observer.Events.Count);
        Assert.Equal("Routing", observer.Events[0].Stage);
        Assert.Equal(response.Status.ToString(), observer.Events[1].Stage);
    }

    [Fact]
    public void DiscoverCapabilities_DelegatesToCatalog()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        var service = NewService(runtime);

        Assert.Equal(8, service.DiscoverCapabilities().Count);
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }

    private sealed class CountingRoutingBehavior : IBehavior
    {
        private readonly IBehavior _inner = new ConversationIntakeAndRoutingBehavior();
        public int InvocationCount { get; private set; }

        public object? Execute(RuntimeExecutionContext context)
        {
            InvocationCount++;
            return _inner.Execute(context);
        }
    }

    private sealed class RecordingProgressObserver : IExecutionProgressObserver
    {
        public List<ExecutionProgressEvent> Events { get; } = new();
        public void OnProgress(ExecutionProgressEvent progressEvent) => Events.Add(progressEvent);
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run: `dotnet test tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj --filter "FullyQualifiedName~AgentExecutionServiceTests"`
Expected: build error — `IAgentExecutionInterface`/`AgentExecutionService` do not exist yet.

- [ ] **Step 3: Write `IAgentExecutionInterface`**

`src/Runtime.Agent/IAgentExecutionInterface.cs`:
```csharp
using Runtime.Agent.Capabilities;

namespace Runtime.Agent;

public interface IAgentExecutionInterface
{
    ExecutionResponse Execute(ExecutionRequest request);
    IReadOnlyList<CapabilityDescriptor> DiscoverCapabilities();
    ExecutionSession StartSession(string? projectProfileId);
}
```

- [ ] **Step 4: Write `AgentExecutionService`**

`src/Runtime.Agent/AgentExecutionService.cs`:
```csharp
using Runtime.Agent.Capabilities;
using Runtime.Agent.Sessions;
using Runtime.Execution;

namespace Runtime.Agent;

// The single seam into governed execution (ADR-0025): the only caller of
// EngineeringExecutionPipeline.Execute anywhere in this assembly. Execute
// is synchronous, matching the pipeline's own synchronous signature.
public sealed class AgentExecutionService : IAgentExecutionInterface
{
    private readonly EngineeringExecutionPipeline _pipeline;
    private readonly ICapabilityCatalog _capabilityCatalog;
    private readonly ISessionStore _sessionStore;

    public AgentExecutionService(EngineeringExecutionPipeline pipeline, ICapabilityCatalog capabilityCatalog, ISessionStore sessionStore)
    {
        _pipeline = pipeline;
        _capabilityCatalog = capabilityCatalog;
        _sessionStore = sessionStore;
    }

    public IReadOnlyList<CapabilityDescriptor> DiscoverCapabilities() => _capabilityCatalog.Discover();

    public ExecutionSession StartSession(string? projectProfileId) => _sessionStore.Create(projectProfileId);

    public ExecutionResponse Execute(ExecutionRequest request)
    {
        var startedAt = DateTimeOffset.UtcNow;

        ExecutionSession? session = null;
        if (request.SessionId is not null)
        {
            session = _sessionStore.Get(request.SessionId);
            if (session is null)
            {
                return UnknownSessionResponse(request, startedAt);
            }
        }

        // Explicit fields on the request always win: an empty
        // ProjectProfileId (there is no nullable variant of this required
        // field) or a null Payload defer to the session's stored default.
        var effectiveProjectProfileId = !string.IsNullOrEmpty(request.ProjectProfileId)
            ? request.ProjectProfileId
            : session?.LastProjectProfileId ?? request.ProjectProfileId;
        var effectivePayload = request.Payload ?? session?.LastPayload;

        request.ProgressObserver?.OnProgress(new ExecutionProgressEvent(
            request.RequestId, "Routing", "Routing request through the Engineering Execution Pipeline.", DateTimeOffset.UtcNow));

        var executionResult = _pipeline.Execute(new EngineeringRequest(request.UserRequest, effectiveProjectProfileId, effectivePayload));

        if (session is not null)
        {
            session = _sessionStore.Touch(session.SessionId, effectiveProjectProfileId, effectivePayload);
        }

        var response = MapToResponse(request, session, executionResult, startedAt, DateTimeOffset.UtcNow);

        request.ProgressObserver?.OnProgress(new ExecutionProgressEvent(
            request.RequestId, response.Status.ToString(), "Execution finished.", DateTimeOffset.UtcNow));

        return response;
    }

    private static ExecutionResponse UnknownSessionResponse(ExecutionRequest request, DateTimeOffset startedAt)
    {
        var completedAt = DateTimeOffset.UtcNow;
        return new ExecutionResponse(
            request.RequestId,
            request.SessionId,
            ExecutionStatus.Failed,
            new ExecutionError("Unknown session.", new[] { $"No session exists for id '{request.SessionId}'." }),
            null,
            null,
            new ExecutionEvidence(Array.Empty<string>(), null, null, null, null),
            new ExecutionMetadata(null, null, completedAt - startedAt, startedAt, completedAt));
    }

    // Kind == NeedsClarification                                -> NeedsClarification
    // Kind == Completed || (Unspecified && Success)              -> Completed
    // Kind == Failed    || (Unspecified && !Success)             -> Failed
    private static ExecutionStatus MapStatus(ExecutionResultKind kind, bool success) => kind switch
    {
        ExecutionResultKind.NeedsClarification => ExecutionStatus.NeedsClarification,
        ExecutionResultKind.Completed => ExecutionStatus.Completed,
        ExecutionResultKind.Failed => ExecutionStatus.Failed,
        _ => success ? ExecutionStatus.Completed : ExecutionStatus.Failed,
    };

    private static ExecutionResponse MapToResponse(ExecutionRequest request, ExecutionSession? session, ExecutionResult result, DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        var status = MapStatus(result.Kind, result.Success);
        var error = status == ExecutionStatus.Completed
            ? null
            : new ExecutionError(result.PresentedResult ?? "Execution did not complete.", result.Diagnostics);

        var evidence = new ExecutionEvidence(result.Diagnostics, result.ValidationResults, result.RepositoryAnalyzed, result.FilesChanged, result.TestsExecuted);
        var metadata = new ExecutionMetadata(result.ExecutedWorkflowId, result.ExecutedAgentId, result.Duration, startedAt, completedAt);

        return new ExecutionResponse(
            request.RequestId,
            session?.SessionId,
            status,
            error,
            result.PresentedResult,
            result.SuggestedNextStep,
            evidence,
            metadata);
    }
}
```

- [ ] **Step 5: Run the tests to confirm they pass**

Run: `dotnet test tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj --filter "FullyQualifiedName~AgentExecutionServiceTests"`
Expected: 7 pass.

- [ ] **Step 6: Run the full `Runtime.Agent.Tests` project**

Run: `dotnet test tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj`
Expected: all pass (Tasks 2-6 combined: 3 + 4 + 2 + 5 + 7 = 21 tests).

- [ ] **Step 7: Commit**

```bash
git add src/Runtime.Agent/IAgentExecutionInterface.cs src/Runtime.Agent/AgentExecutionService.cs tests/Runtime.Agent.Tests/AgentExecutionServiceTests.cs
git commit -m "feat(runtime.agent): add AgentExecutionService, the router into EngineeringExecutionPipeline"
```

---

## Task 7: Dependency Injection

**Files:**
- Modify: `src/Host/Program.cs`
- Modify: `src/Host/AefPlatform.Host.csproj`
- Test: `tests/Host.Tests/GA2AgentExecutionHostTests.cs`

**Interfaces:**
- Consumes: `AgentExecutionService`, `EngineeringAgentCapabilityCatalog`, `InMemorySessionStore` (Tasks 4-6), `EngineeringExecutionPipeline` (already registered in `Program.cs`).
- Produces: `IAgentExecutionInterface` resolvable from the DI container — the seam any future GA-3 channel host will resolve from.

- [ ] **Step 1: Write the failing test**

`tests/Host.Tests/GA2AgentExecutionHostTests.cs`:
```csharp
using AefPlatform.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Runtime.Agent;
using Runtime.Agent.Capabilities;
using Runtime.Agent.Sessions;
using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Ai.Providers;
using Runtime.Bootstrapping;
using Runtime.DomainContent.ConversationManagement;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.DomainContent.EngineeringPlanning;
using Runtime.DomainContent.EngineeringSpecificationReview;
using Runtime.DomainContent.ImplementationValidation;
using Runtime.DomainContent.RepositoryIntelligence;
using Runtime.DomainContent.RepositoryValidation;
using Runtime.Execution;
using Runtime.Hosts.Skill;

namespace Host.Tests;

// Mirrors Program.cs's own DI wiring, extended with GA-2's three new
// registrations - proves IAgentExecutionInterface resolves cleanly from
// the real composition graph, the same convention GA1AiIntegrationHostTests
// already established for the AI Integration Layer.
public class GA2AgentExecutionHostTests
{
    private static HostApplicationBuilder CreateBuilder(IDictionary<string, string?> configOverrides)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(configOverrides);

        builder.Services
            .AddOptions<PlatformHostOptions>()
            .Bind(builder.Configuration.GetSection("AefPlatform"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services
            .AddOptions<AiProviderOptions>()
            .Bind(builder.Configuration.GetSection("Ai"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton(new HttpClient());

        builder.Services.AddSingleton<IAiProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiProviderOptions>>().Value;
            var httpClient = sp.GetRequiredService<HttpClient>();
            return options.Provider switch
            {
                "AzureOpenAI" => new AzureOpenAiProvider(httpClient, new AzureOpenAiProviderOptions
                {
                    Endpoint = options.AzureOpenAi!.Endpoint,
                    ApiKey = options.AzureOpenAi.ApiKey,
                    Deployment = options.AzureOpenAi.Deployment,
                    ApiVersion = options.AzureOpenAi.ApiVersion,
                }),
                _ => new OpenAiProvider(httpClient, new OpenAiProviderOptions
                {
                    ApiKey = options.OpenAi!.ApiKey,
                    Model = options.OpenAi.Model,
                    BaseUrl = options.OpenAi.BaseUrl,
                }),
            };
        });

        builder.Services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiProviderOptions>>().Value;
            return new AiExecutionService(sp.GetRequiredService<IAiProvider>(), timeout: TimeSpan.FromSeconds(options.TimeoutSeconds));
        });

        builder.Services.AddSingleton<IPromptCatalog, EngineeringPromptCatalog>();

        builder.Services.AddSingleton(sp =>
        {
            IReadOnlyDictionary<string, IBehavior> behaviors = new Dictionary<string, IBehavior>
            {
                ["SKL-EXE-0018"] = new DeliveryImplementationSkillBehavior(),
                ["SKL-EXE-0019"] = new DeliveryValidationSkillBehavior(),
                ["SKL-EXE-0020"] = new DeliveryDocumentationSkillBehavior(),
                ["SKL-EXE-0021"] = new ConversationIntakeAndRoutingBehavior(),
                ["SKL-EXE-0022"] = new ConversationHandoffAndFollowupBehavior(),
                ["SKL-EXE-0023"] = new EngineeringDiscoveryAndGoalClarificationBehavior(),
                ["SKL-EXE-0024"] = new SpecificationDraftingAndAlignmentBehavior(),
                ["SKL-EXE-0025"] = new ReadinessClassificationAndApprovalRequestBehavior(),
                ["SKL-EXE-0026"] = new SpecificationIndependentReviewBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
                ["SKL-EXE-0027"] = new SpecificationConformanceValidationBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
                ["SKL-EXE-0028"] = new ArchitectureStandardsConformanceReviewBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
                ["SKL-EXE-0029"] = new ValidationFindingsSynthesisBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
                ["SKL-EXE-0030"] = new RepositoryAndArchitectureUnderstandingBehavior(),
                ["SKL-EXE-0031"] = new CodeIntelligenceAndQualitySignalDetectionBehavior(),
                ["SKL-EXE-0032"] = new EngineeringDiagnosticsAndRootCauseAnalysisBehavior(),
                ["SKL-EXE-0033"] = new RecommendationSynthesisAndEvidenceBasedReportingBehavior(),
                ["REPO-VALIDATION"] = new RepositoryValidationSkillBehavior(),
            };

            return RuntimeBootstrapper.Boot(
                new RuntimeConfiguration(PolicyResolver: new PermissiveActivationPolicyResolver()),
                behaviors);
        });
        builder.Services.AddSingleton(sp => new EngineeringExecutionPipeline(sp.GetRequiredService<RuntimeHandle>()));
        builder.Services.AddSingleton<ICapabilityCatalog, EngineeringAgentCapabilityCatalog>();
        builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
        builder.Services.AddSingleton<IAgentExecutionInterface>(sp => new AgentExecutionService(
            sp.GetRequiredService<EngineeringExecutionPipeline>(),
            sp.GetRequiredService<ICapabilityCatalog>(),
            sp.GetRequiredService<ISessionStore>()));
        builder.Services.AddHostedService<RuntimeStartupHostedService>();

        return builder;
    }

    [Fact]
    public async Task StartAsync_ResolvesIAgentExecutionInterface()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["AefPlatform:PlatformName"] = "AEF Platform Runtime",
            ["Ai:Provider"] = "OpenAI",
            ["Ai:OpenAi:ApiKey"] = "",
            ["Ai:OpenAi:Model"] = "gpt-4o",
        });
        using var host = builder.Build();

        await host.StartAsync();
        var agentExecutionInterface = host.Services.GetRequiredService<IAgentExecutionInterface>();

        Assert.NotNull(agentExecutionInterface);
        await host.StopAsync();
    }

    [Fact]
    public async Task StartAsync_DiscoverCapabilities_ReturnsTheHandAuthoredEightCapabilities()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["AefPlatform:PlatformName"] = "AEF Platform Runtime",
            ["Ai:Provider"] = "OpenAI",
            ["Ai:OpenAi:ApiKey"] = "",
            ["Ai:OpenAi:Model"] = "gpt-4o",
        });
        using var host = builder.Build();

        await host.StartAsync();
        var agentExecutionInterface = host.Services.GetRequiredService<IAgentExecutionInterface>();

        Assert.Equal(8, agentExecutionInterface.DiscoverCapabilities().Count);
        await host.StopAsync();
    }

    [Fact]
    public async Task StartAsync_StartSession_ReturnsANewSessionWithASessionId()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["AefPlatform:PlatformName"] = "AEF Platform Runtime",
            ["Ai:Provider"] = "OpenAI",
            ["Ai:OpenAi:ApiKey"] = "",
            ["Ai:OpenAi:Model"] = "gpt-4o",
        });
        using var host = builder.Build();

        await host.StartAsync();
        var agentExecutionInterface = host.Services.GetRequiredService<IAgentExecutionInterface>();
        var session = agentExecutionInterface.StartSession(null);

        Assert.False(string.IsNullOrEmpty(session.SessionId));
        await host.StopAsync();
    }
}
```

- [ ] **Step 2: Run the test to confirm it fails**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj --filter "FullyQualifiedName~GA2AgentExecutionHostTests"`
Expected: build error — `Host.Tests.csproj` has no reference to `Runtime.Agent`, and `Program.cs` does not yet register `IAgentExecutionInterface`.

- [ ] **Step 3: Add the `Runtime.Agent` reference to `Host`**

`src/Host/AefPlatform.Host.csproj` — add a second `ProjectReference`:
```xml
  <ItemGroup>
    <ProjectReference Include="..\Runtime\Runtime.csproj" />
    <ProjectReference Include="..\Runtime.Agent\Runtime.Agent.csproj" />
  </ItemGroup>
```

`tests/Host.Tests/Host.Tests.csproj` — add the same reference alongside the existing three:
```xml
  <ItemGroup>
    <ProjectReference Include="..\..\src\Host\AefPlatform.Host.csproj" />
    <ProjectReference Include="..\..\src\Runtime\Runtime.csproj" />
    <ProjectReference Include="..\..\src\Runtime.Agent\Runtime.Agent.csproj" />
    <ProjectReference Include="..\..\src\Kernel\Kernel.csproj" />
  </ItemGroup>
```

- [ ] **Step 4: Wire the DI registrations into `Program.cs`**

`src/Host/Program.cs` — add three `using` statements alongside the existing `Runtime.*` usings:
```csharp
using Runtime.Agent;
using Runtime.Agent.Capabilities;
using Runtime.Agent.Sessions;
```

Then, immediately after the existing line:
```csharp
builder.Services.AddSingleton(sp => new EngineeringExecutionPipeline(sp.GetRequiredService<RuntimeHandle>()));
```
add:
```csharp
builder.Services.AddSingleton<ICapabilityCatalog, EngineeringAgentCapabilityCatalog>();
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddSingleton<IAgentExecutionInterface>(sp => new AgentExecutionService(
    sp.GetRequiredService<EngineeringExecutionPipeline>(),
    sp.GetRequiredService<ICapabilityCatalog>(),
    sp.GetRequiredService<ISessionStore>()));
```
(before the existing `builder.Services.AddHostedService<RuntimeStartupHostedService>();` line). No configuration section is needed — there is nothing to configure.

- [ ] **Step 5: Run the test to confirm it passes**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj --filter "FullyQualifiedName~GA2AgentExecutionHostTests"`
Expected: 3 pass.

- [ ] **Step 6: Run the full `Host.Tests` project**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj`
Expected: all pass, no regression in the pre-existing Host test suite (GA-1's `GA1AiIntegrationHostTests`, `PlatformHostStartupTests`, etc.).

- [ ] **Step 7: Commit**

```bash
git add src/Host/Program.cs src/Host/AefPlatform.Host.csproj tests/Host.Tests/Host.Tests.csproj tests/Host.Tests/GA2AgentExecutionHostTests.cs
git commit -m "feat(host): wire AgentExecutionService, EngineeringAgentCapabilityCatalog, and InMemorySessionStore into DI"
```

---

## Task 8: Documentation

**Files:**
- Modify: `README.md`
- Modify: `docs/superpowers/specs/2026-07-08-ga2-ai-agent-integration-layer-design.md`

**Interfaces:** None — documentation only, no code change.

This repo's established convention (RC0, P1-P4, GA-1) is a single dense Status paragraph in `README.md` per completed stage, prepended above the prior entry, citing this plan document for full detail — not a set of separate architecture/component-diagram/interfaces files. `## Layout` (below `## Status`) has been frozen since the Epic-0006/CU-3-Hardening-Sprint entries and was not extended for RC0/P1-P4/GA-1 either; this task does not extend it, matching that established precedent rather than inventing a new one.

- [ ] **Step 1: Add the GA-2 Status paragraph**

`README.md` — insert immediately after the `## Status` heading (line 7), as the new first paragraph, above the existing `**AEF v1.0 RC0 tagged...**` paragraph:

```markdown
**AEF v1.0 GA-2 (AI Agent Integration Layer) complete, 2026-07-08.** Builds the first stable, product-facing execution interface over `EngineeringExecutionPipeline` — see `docs/superpowers/specs/2026-07-08-ga2-ai-agent-integration-layer-design.md` for the full design and `docs/superpowers/plans/2026-07-08-ga2-ai-agent-integration-layer-implementation-plan.md` for the phase-by-phase account. New class library `src/Runtime.Agent` references `Runtime` only — never `Kernel`, never `Runtime.Ai` — a compile-time-adjacent guarantee proven by an automated assembly-reference test (`ArchitectureBoundaryTests`), realizing `ADR-0025` (`../ai-engineering-framework/epics/decisions/0025-public-execution-interface-boundary.md`): `Runtime.Agent` is the sole public execution boundary, `EngineeringExecutionPipeline` stays internal, and every future channel (GA-3's CLI/REST/IDE/Chat) must depend on `Runtime.Agent` only. `Runtime.Execution.ExecutionResult` gained one additive field, `Kind` (`ExecutionResultKind`), resolving a real, previously-provable ambiguity — `NeedsClarification` and a genuinely failed dispatch were indistinguishable to any caller before this change. `AgentExecutionService` (`IAgentExecutionInterface`) is the single new caller of `EngineeringExecutionPipeline.Execute`, exposing request/response contracts (`ExecutionRequest`/`ExecutionResponse`), a hand-authored `EngineeringAgentCapabilityCatalog` (one descriptor per capability id the pipeline already dispatches to), an `InMemorySessionStore` (process-lifetime only, never persisted — session state is caller convenience and never influences routing or dispatch), and a minimal `IExecutionProgressObserver` extension point (`Routing` + one terminal event per call). Wired into the live `Host` via three new DI singletons — no configuration section, since there is nothing to configure. No live CLI/REST/IDE/Chat endpoint exists yet — GA-3's job. No `Runtime.Ai` change, no `EngineeringExecutionPipeline` redesign, no new NuGet dependency.
```

- [ ] **Step 2: Update the design spec's Status line**

`docs/superpowers/specs/2026-07-08-ga2-ai-agent-integration-layer-design.md` — replace line 3:
```markdown
**Status:** Approved (design), pending implementation plan.
```
with:
```markdown
**Status:** Implemented. See `docs/superpowers/plans/2026-07-08-ga2-ai-agent-integration-layer-implementation-plan.md` for the implementation plan and `README.md` for the completion summary.
```

- [ ] **Step 3: Commit**

```bash
git add README.md docs/superpowers/specs/2026-07-08-ga2-ai-agent-integration-layer-design.md
git commit -m "docs(ga2): record GA-2 (AI Agent Integration Layer) completion"
```

---

## Task 9: Test completion

**Files:** None (verification only).

- [ ] **Step 1: Capture the pre-GA-2 baseline, if not already known**

Run (on `main`, before Task 1, or via `git stash` if run mid-branch): `dotnet test AefPlatform.sln`
Record the total pass count as `N` (the last-known baseline was 324 at RC0, before GA-1's own additions — GA-1 added more tests across `Runtime.Ai.Tests`, `Runtime.Tests`, `Host.Tests` that are not separately re-counted here; `N` is whatever `main` reports today).

- [ ] **Step 2: Run the full solution test suite on the feature branch**

Run: `dotnet build AefPlatform.sln` then `dotnet test AefPlatform.sln`
Expected: 0 build warnings, 0 test failures, total pass count = `N` (baseline) + 28 (this plan's new tests: 4 in Task 1 (`ExecutionResultKindTests` x2 + 2 new `EngineeringExecutionPipelineTests` facts) + 3 in Task 2 + 4 in Task 3 + 2 in Task 4 + 5 in Task 5 + 7 in Task 6 + 3 in Task 7 (Host.Tests) — Task 8 adds no tests).

- [ ] **Step 3: Confirm no pre-existing test regressed**

Run: `dotnet test AefPlatform.sln --filter "FullyQualifiedName!~Runtime.Agent.Tests&FullyQualifiedName!~ExecutionResultKindTests&FullyQualifiedName!~GA2AgentExecutionHostTests&FullyQualifiedName!~Execute_UnroutableRequest_SetsKindToNeedsClarification&FullyQualifiedName!~Execute_WorkflowNotRegistered_KindStaysUnspecified"`
Expected: pass count = `N` exactly (every pre-existing test, GA-2 tests excluded by filter, is unaffected by the additive `ExecutionResult.Kind` field and the new `Runtime.Agent` assembly).

- [ ] **Step 4: Record the final total**

Note the final `dotnet test AefPlatform.sln` pass count for use in Task 10's PR description.

---

## Task 10: Final validation

**Files:** None (release mechanics only).

- [ ] **Step 1: Confirm working tree is clean**

Run: `git status`
Expected: nothing to commit (all 7 feature commits from Tasks 1-8 already made).

- [ ] **Step 2: Push and open the PR**

```bash
git push -u origin feature/ga2-ai-agent-integration-layer
gh pr create --title "GA-2: AI Agent Integration Layer" --body "$(cat <<'EOF'
## Summary
- New class library Runtime.Agent (references Runtime only, never Kernel/Runtime.Ai — ADR-0025, enforced by an automated assembly-reference test), the sole public execution boundary for aef-platform's governed engineering capabilities.
- AgentExecutionService (IAgentExecutionInterface) is the single new caller of EngineeringExecutionPipeline.Execute: request/response contracts, a hand-authored 8-entry EngineeringAgentCapabilityCatalog, an in-memory, process-lifetime-only ISessionStore, and a minimal IExecutionProgressObserver extension point.
- One additive field on Runtime.Execution.ExecutionResult (Kind: ExecutionResultKind), resolving the previously-provable ambiguity between NeedsClarification and a genuine dispatch failure. Exactly one existing call site changed.
- Wired into the live Host via three new DI singletons. No configuration section, no new NuGet dependency, no live CLI/REST/IDE/Chat endpoint (GA-3's job).

## Test plan
- [ ] `dotnet test AefPlatform.sln` green, full pre-existing suite unaffected
- [ ] New Runtime.Agent.Tests (21 tests), Runtime.Tests additions (3 tests), Host.Tests additions (3 tests) all green
EOF
)"
```

- [ ] **Step 3: Merge once CI/self-review is clean**

Per this repo's own standing convention (no separate per-PR confirmation once review is clean): `gh pr merge --auto --squash` (or plain `gh pr merge --squash` if auto-merge isn't enabled on this repo).

- [ ] **Step 4: Update local `main` and delete the merged branch**

```bash
git checkout main
git pull
git branch -d feature/ga2-ai-agent-integration-layer
```

- [ ] **Step 5: Confirm the merged state**

Run: `dotnet test AefPlatform.sln` on `main`
Expected: same pass count recorded in Task 9 Step 4, 0 warnings, 0 failures.

---

## Summary

GA-2 adds one new class library (`Runtime.Agent`, referencing `Runtime` only), one additive field on `Runtime.Execution.ExecutionResult` (`Kind`), and three new DI registrations in `Host` — no transport, no redesign, no new dependency. `AgentExecutionService` is now the one and only public, product-facing way into governed engineering execution, per `ADR-0025`, ready for GA-3 to build the first live channel on top of.

## Test Plan

- Task 1: `ExecutionResultKindTests` (2) + 2 new facts in `EngineeringExecutionPipelineTests` = 4 new `Runtime.Tests`.
- Tasks 2-6: `ArchitectureBoundaryTests` (3) + `ExecutionContractsTests` (4) + `EngineeringAgentCapabilityCatalogTests` (2) + `InMemorySessionStoreTests` (5) + `AgentExecutionServiceTests` (7) = 21 new `Runtime.Agent.Tests`.
- Task 7: `GA2AgentExecutionHostTests` (3) new `Host.Tests`.
- Task 9: full-solution gate, baseline + 28, 0 regressions.
