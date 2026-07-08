# GA-1: AI Integration Layer & Engineering Intelligence Runtime — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a provider-neutral AI Integration Layer (OpenAI + Azure OpenAI) and a Prompt Execution Engine to `aef-platform`, and use it to give `AGT-EXE-0013` (Engineering Specification Reviewer) and `AGT-EXE-0014` (Validation Agent) real, non-fabricated implementations — the two capabilities RC0's own "Recommendation for GA Readiness" named as blocked on "no LLM-invocation abstraction."

**Architecture:** New `Runtime.Ai` class library (referenced by `Runtime`, itself referencing only `Kernel`) holds the provider-neutral abstraction (`IAiProvider`), the retry/timeout/diagnostics orchestrator (`AiExecutionService`, built on the existing `RetryExecutor` and `IEvidenceSink` — no new resilience or evidence mechanism), the Prompt Execution Engine (template + renderer + structured-response parser), and two concrete providers (`OpenAiProvider`, `AzureOpenAiProvider`) built on plain `HttpClient`/`System.Text.Json` — no new NuGet dependency, matching the repo's existing hand-rolled-over-third-party convention (no Polly, no JSON-schema library today). Four new `IBehavior` realizations (`SKL-EXE-0026/27/28/29`) are wired into `EngineeringExecutionPipeline` via two new dispatch cases (`WFL-DEL-0007`, `WFL-DEL-0005`) that follow the exact `InvokeSkill<T>` chaining pattern already used by `FromEngineeringPlanning`. Deterministic dispatch (`WFL-DEL-0001/0004/0006/0008`, `SKL-EXE-0018`, `REPO-VALIDATION`) is untouched.

**Tech Stack:** .NET 8, xunit, `System.Net.Http`/`System.Text.Json` (BCL only, no new packages), `Microsoft.Extensions.Options` (already used by Host).

## Global Constraints

- No new NuGet packages. Providers call the OpenAI/Azure OpenAI REST APIs directly via `HttpClient` — avoids an unverifiable `dotnet restore` against the OpenAI/Azure SDKs in this environment and matches the repo's zero-extra-dependency convention (verified: `Kernel.csproj`/`Runtime.csproj` have no `PackageReference` at all; `Host.csproj` only has `Microsoft.Extensions.Hosting`/`Options.DataAnnotations`).
- `Runtime` and `Kernel` have no DI/Configuration reference today — this plan does not add one. Provider configuration (`OpenAiProviderOptions`/`AzureOpenAiProviderOptions`) is bound via `IOptions<T>` **only in `Host`**, then passed down through plain constructor injection (matching `DeliveryImplementationSkillBehavior`'s `Func<...>` factory-delegate pattern), never referenced directly from `Runtime`/`Runtime.Ai`.
- Every `IBehavior.Execute` never throws on a recognized failure — it returns an "escalated"/"terminal_failure"-shaped result object, per every existing behavior in the repo (`DeliveryImplementationSkillBehavior`, `ConversationIntakeAndRoutingBehavior`, etc.) and per `AGT-EXE-0013`/`AGT-EXE-0014`'s own governed Pause-reason text ("Terminal Failure is reserved exclusively for structural failures... never for engineering disagreement").
- Reuse, don't duplicate: retry → `Runtime.Orchestration.Resilience.RetryExecutor` (existing); diagnostics/audit → `Kernel.Evidence.IEvidenceSink` (existing); dispatch pattern → `EngineeringExecutionPipeline.InvokeSkill<T>` (existing, unmodified).
- Deterministic execution must keep passing: the existing 324 tests must stay green throughout, and are re-run as the final gate before merge.
- Prompt content is not invented — the four prompt templates render the exact Input/Output/Preconditions/Postconditions text already frozen in `ai-engineering-framework`'s `PCT-EXE-0026`/`0027`/`0028`/`0029` (quoted in this plan's tasks verbatim from that repo, read 2026-07-08).

---

## Phase 0 — Gap Analysis (completed 2026-07-08, recorded here for the record)

| Capability | Existing Implementation | Gap | Action |
|---|---|---|---|
| AI Provider abstraction | None — zero occurrences of `OpenAI`/`Azure`/`ChatCompletion`/`IAiProvider` anywhere in `aef-platform` (verified by grep across `src`+`tests`) | Total | **Implement** |
| AI Execution Service (retry/timeout/cancellation/diagnostics) | `RetryExecutor` (retry), `IEvidenceSink` (diagnostics audit) exist and are reused as-is; no timeout/cancellation wrapper exists anywhere (ad-hoc `Process.WaitForExit` timeouts only, no generic version) | Orchestration layer combining the two, plus timeout/cancellation | **Extend** (retry, evidence) + **Implement** (timeout/cancellation orchestration, token accounting) |
| Prompt loading/rendering/variable substitution | None. "Prompt" only appears as the governance term "Prompt Contract" in docs; no runtime prompt-template concept exists | Total | **Implement** |
| Structured response parsing / schema validation | None. No JSON-schema library referenced; `System.Text.Json` available via BCL only | Total | **Implement** (hand-rolled, matching the repo's no-Polly/no-schema-lib convention) |
| OpenAI provider | None | Total | **Implement** |
| Azure OpenAI provider | None | Total | **Implement** |
| Runtime integration — dispatch seam | `EngineeringExecutionPipeline.Dispatch` is a `switch` on capability ID → private `From*` methods; `InvokeSkill<T>` is the exact seam every Skill chain already uses (`FromEngineeringPlanning`) | Two capability IDs (`WFL-DEL-0007`, `WFL-DEL-0005`) have no dispatch target yet | **Reuse** the seam, **Implement** the two new `From*` methods |
| Conversation Manager routing | `ConversationIntakeAndRoutingBehavior` (`SKL-EXE-0021`), deterministic keyword table | No keywords route to the two new capabilities | **Extend** (append two keyword entries) |
| `AGT-EXE-0013` Engineering Specification Reviewer | Governed spec only (`ai-engineering-framework` `AGT-EXE-0013`/`SKL-EXE-0026`/`PCT-EXE-0026`, all `Active`); zero runtime code in `aef-platform` | Total (code) | **Implement** |
| `AGT-EXE-0014` Validation Agent | Governed spec only (`SKL-EXE-0027/28/29`, `PCT-EXE-0027/28/29`, all `Active`); zero runtime code | Total (code) | **Implement** |
| `AGT-EXE-0012` Engineering Planner | Already implemented, deterministic (`SKL-EXE-0023/24/25`), 324/324 tests passing through it | GA-1 does not require upgrading it — RC0's own Known Limitations name only `0013`/`0014` as the GA-blocking gap | **Reuse**, unchanged |
| Manifest / registry | `EngineeringArchitectureManifest.KnownArtifacts` | Four new artifact IDs (`SKL-EXE-0026/27/28/29`) not listed | **Extend** |
| Configuration convention | `IOptions<PlatformHostOptions>` bound in `Host` via `.Bind().ValidateDataAnnotations().ValidateOnStart()` | No AI provider config section | **Extend** (new `AiProviderOptions`, same pattern) |
| Tests | 324/324 passing, xunit, 3 test projects | No AI-layer coverage | **Implement** |

---

## File Structure

```
src/Runtime.Ai/Runtime.Ai.csproj                                   (new project, net8.0, ProjectRef -> Kernel)
src/Runtime.Ai/IAiProvider.cs
src/Runtime.Ai/AiPromptRequest.cs
src/Runtime.Ai/AiCompletionResult.cs
src/Runtime.Ai/AiUsage.cs
src/Runtime.Ai/AiProviderException.cs
src/Runtime.Ai/AiExecutionOutcome.cs
src/Runtime.Ai/AiExecutionService.cs
src/Runtime.Ai/Prompts/PromptTemplate.cs
src/Runtime.Ai/Prompts/PromptRenderResult.cs
src/Runtime.Ai/Prompts/PromptRenderer.cs
src/Runtime.Ai/Prompts/IPromptCatalog.cs
src/Runtime.Ai/Prompts/EngineeringPromptCatalog.cs
src/Runtime.Ai/StructuredResponseParser.cs
src/Runtime.Ai/Providers/OpenAiProviderOptions.cs
src/Runtime.Ai/Providers/OpenAiProvider.cs
src/Runtime.Ai/Providers/AzureOpenAiProviderOptions.cs
src/Runtime.Ai/Providers/AzureOpenAiProvider.cs

src/Runtime/Runtime.csproj                                         (modify: add ProjectRef -> Runtime.Ai)
src/Runtime/DomainContent/EngineeringSpecificationReview/EngineeringSpecificationReviewTypes.cs   (new)
src/Runtime/DomainContent/EngineeringSpecificationReview/SpecificationIndependentReviewBehavior.cs (new, SKL-EXE-0026)
src/Runtime/DomainContent/ImplementationValidation/ImplementationValidationTypes.cs               (new)
src/Runtime/DomainContent/ImplementationValidation/SpecificationConformanceValidationBehavior.cs   (new, SKL-EXE-0027)
src/Runtime/DomainContent/ImplementationValidation/ArchitectureStandardsConformanceReviewBehavior.cs (new, SKL-EXE-0028)
src/Runtime/DomainContent/ImplementationValidation/ValidationFindingsSynthesisBehavior.cs          (new, SKL-EXE-0029)
src/Runtime/Execution/EngineeringExecutionPipeline.cs               (modify: two new switch cases + two new From* methods)
src/Runtime/DomainContent/ConversationManagement/ConversationIntakeAndRoutingBehavior.cs (modify: two new keyword entries)

src/Host/Program.cs                                                 (modify: bind AiProviderOptions, construct provider, wire 4 new behaviors)
src/Host/AiProviderOptions.cs                                       (new)
src/Host/AefPlatform.Host.csproj                                    (modify: ProjectRef already transitive via Runtime -> Runtime.Ai, no direct change needed)
src/Host/EngineeringArchitectureManifest.cs                          (modify: 4 new artifact entries)
src/Host/appsettings.json                                           (modify: add "Ai" section with safe defaults, no real secrets)

tests/Runtime.Ai.Tests/Runtime.Ai.Tests.csproj                      (new test project)
tests/Runtime.Ai.Tests/AiExecutionServiceTests.cs
tests/Runtime.Ai.Tests/PromptRendererTests.cs
tests/Runtime.Ai.Tests/StructuredResponseParserTests.cs
tests/Runtime.Ai.Tests/Providers/OpenAiProviderTests.cs
tests/Runtime.Ai.Tests/Providers/AzureOpenAiProviderTests.cs
tests/Runtime.Tests/Execution/EngineeringExecutionPipelineAiTests.cs (new file in existing project)
tests/Runtime.Tests/DomainContent/EngineeringSpecificationReview/SpecificationIndependentReviewBehaviorTests.cs (new)
tests/Runtime.Tests/DomainContent/ImplementationValidation/ImplementationValidationBehaviorTests.cs (new)
tests/Host.Tests/GA1AiIntegrationHostTests.cs                        (new)
AefPlatform.sln                                                      (modify: add Runtime.Ai + Runtime.Ai.Tests projects)
```

---

## Task 1: `Runtime.Ai` project skeleton + core abstraction types

**Files:**
- Create: `src/Runtime.Ai/Runtime.Ai.csproj`
- Create: `src/Runtime.Ai/AiUsage.cs`
- Create: `src/Runtime.Ai/AiPromptRequest.cs`
- Create: `src/Runtime.Ai/AiCompletionResult.cs`
- Create: `src/Runtime.Ai/AiProviderException.cs`
- Create: `src/Runtime.Ai/IAiProvider.cs`
- Modify: `AefPlatform.sln` (add project)
- Test: `tests/Runtime.Ai.Tests/Runtime.Ai.Tests.csproj`, `tests/Runtime.Ai.Tests/AiPromptRequestTests.cs`

**Interfaces:**
- Produces: `IAiProvider.CompleteAsync(AiPromptRequest, CancellationToken) : Task<AiCompletionResult>` — every later provider/service task depends on this exact signature.
- Produces: `AiPromptRequest(string PromptContractId, string SystemPrompt, string UserPrompt, double Temperature = 0.0, int MaxOutputTokens = 2000)`.
- Produces: `AiCompletionResult(string RawText, AiUsage Usage, string ProviderName, string Model)`.
- Produces: `AiUsage(int PromptTokens, int CompletionTokens, int TotalTokens)`.
- Produces: `AiProviderException(string Message, bool IsTransient, Exception? Inner = null)`.

- [ ] **Step 1: Create the project file**

`src/Runtime.Ai/Runtime.Ai.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Kernel\Kernel.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add the project to the solution**

Run: `dotnet sln AefPlatform.sln add src/Runtime.Ai/Runtime.Ai.csproj`

- [ ] **Step 3: Write the core types**

`src/Runtime.Ai/AiUsage.cs`:
```csharp
namespace Runtime.Ai;

public sealed record AiUsage(int PromptTokens, int CompletionTokens, int TotalTokens);
```

`src/Runtime.Ai/AiPromptRequest.cs`:
```csharp
namespace Runtime.Ai;

// PromptContractId is the governed PCT-EXE-00xx id this call realizes -
// carried through to IEvidenceSink so every AI call is traceable to the
// Prompt Contract that authorized it, matching SkillHost's own
// SubjectId=skillId audit convention.
public sealed record AiPromptRequest(
    string PromptContractId,
    string SystemPrompt,
    string UserPrompt,
    double Temperature = 0.0,
    int MaxOutputTokens = 2000);
```

`src/Runtime.Ai/AiCompletionResult.cs`:
```csharp
namespace Runtime.Ai;

public sealed record AiCompletionResult(string RawText, AiUsage Usage, string ProviderName, string Model);
```

`src/Runtime.Ai/AiProviderException.cs`:
```csharp
namespace Runtime.Ai;

// IsTransient is the one classification decision a provider makes about
// its own failure (network error, 5xx, 429 => transient; 400/401/403,
// malformed request => structural) - AiExecutionService trusts it
// verbatim into RetryExecutor's own FailureClassification, mirroring
// POL-GOV-0001's "the caller supplying this result is the one place
// that classification decision is made" boundary.
public sealed class AiProviderException : Exception
{
    public bool IsTransient { get; }

    public AiProviderException(string message, bool isTransient, Exception? inner = null)
        : base(message, inner)
    {
        IsTransient = isTransient;
    }
}
```

`src/Runtime.Ai/IAiProvider.cs`:
```csharp
namespace Runtime.Ai;

public interface IAiProvider
{
    string Name { get; }

    Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Create the test project**

`tests/Runtime.Ai.Tests/Runtime.Ai.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Runtime.Ai\Runtime.Ai.csproj" />
  </ItemGroup>
</Project>
```

Run: `dotnet sln AefPlatform.sln add tests/Runtime.Ai.Tests/Runtime.Ai.Tests.csproj`

- [ ] **Step 5: Write a failing smoke test**

`tests/Runtime.Ai.Tests/AiPromptRequestTests.cs`:
```csharp
using Runtime.Ai;
using Xunit;

namespace Runtime.Ai.Tests;

public class AiPromptRequestTests
{
    [Fact]
    public void DefaultsAreZeroTemperatureAndTwoThousandTokens()
    {
        var request = new AiPromptRequest("PCT-EXE-0026", "system", "user");

        Assert.Equal(0.0, request.Temperature);
        Assert.Equal(2000, request.MaxOutputTokens);
    }
}
```

- [ ] **Step 6: Run it, confirm it fails to compile/pass, then confirm green**

Run: `dotnet test tests/Runtime.Ai.Tests/Runtime.Ai.Tests.csproj`
Expected: builds and passes (this is a scaffolding task — the "red" state is the project not existing yet; once Steps 1-5 land, this passes immediately).

- [ ] **Step 7: Commit**

```bash
git checkout -b feature/ga1-ai-integration-layer
git add src/Runtime.Ai tests/Runtime.Ai.Tests AefPlatform.sln
git commit -m "feat(ai): scaffold Runtime.Ai project and provider abstraction types"
```

---

## Task 2: `AiExecutionService` — retry, timeout, cancellation, diagnostics

**Files:**
- Create: `src/Runtime.Ai/AiExecutionOutcome.cs`
- Create: `src/Runtime.Ai/AiExecutionService.cs`
- Modify: `src/Runtime.Ai/Runtime.Ai.csproj` (add ProjectRef to `Runtime` for `RetryExecutor`? — **No**: `RetryExecutor` lives in `Runtime`, but `Runtime` will reference `Runtime.Ai`, so `Runtime.Ai` referencing `Runtime` back would be circular. Copy the pattern, don't reference it: `Runtime.Ai` gets its **own** tiny retry loop reusing the exact same algorithm/types shape, OR `RetryExecutor`/`AttemptResult`/`RetryOutcome`/`FailureClassification` move down into `Kernel` so both `Runtime` and `Runtime.Ai` can reference them without a cycle. Moving is the correct "Extend, don't duplicate" choice — see Step 1.)
- Test: `tests/Runtime.Ai.Tests/AiExecutionServiceTests.cs`

**Interfaces:**
- Consumes: `IAiProvider.CompleteAsync` (Task 1), the relocated `Kernel.Orchestration.Resilience.RetryExecutor`/`AttemptResult`/`RetryOutcome`/`FailureClassification` (Step 1 of this task), `Kernel.Evidence.IEvidenceSink`/`EvidenceRecord` (existing, unmodified).
- Produces: `AiExecutionService.Execute(AiPromptRequest, CancellationToken) : AiExecutionOutcome` — consumed by every Task 5/6 behavior.
- Produces: `AiExecutionOutcome` with `bool Success`, `AiCompletionResult? Result`, `string? FailureReason`, `TimeSpan Duration`, `int AttemptCount`, factory methods `Completed(...)`/`Failed(...)`.

- [ ] **Step 1: Move the resilience types down to `Kernel` (no behavior change, only namespace/location)**

Move `src/Runtime/Orchestration/Resilience/{AttemptResult.cs,RetryOutcome.cs,RetryExecutor.cs}` to `src/Kernel/Resilience/` (create the folder), changing only their `namespace` line from `Runtime.Orchestration.Resilience` to `Kernel.Resilience`. Leave `TerminalRecoveryDecision.cs`/`TerminalRecoveryResolver.cs`/`TerminalRecoveryResult.cs` in `Runtime.Orchestration.Resilience` untouched (they are Orchestrator-specific, not reused by `Runtime.Ai`). Update every existing `using Runtime.Orchestration.Resilience;` that referenced the three moved types (find with `grep -rl "Orchestration.Resilience" src tests`) to `using Kernel.Resilience;` instead — the Orchestrator's own files that use `TerminalRecoveryResolver` etc. keep their existing `using Runtime.Orchestration.Resilience;` for those, and add `using Kernel.Resilience;` only where `RetryExecutor`/`AttemptResult`/`RetryOutcome` are actually used.

- [ ] **Step 2: Run the full existing suite to confirm the move is behavior-preserving**

Run: `dotnet test AefPlatform.sln`
Expected: 324/324 still passing (pure move, no logic change).

- [ ] **Step 3: Write the failing test for `AiExecutionService`**

`tests/Runtime.Ai.Tests/AiExecutionServiceTests.cs`:
```csharp
using Kernel.Evidence;
using Runtime.Ai;
using Xunit;

namespace Runtime.Ai.Tests;

public class AiExecutionServiceTests
{
    private sealed class StubProvider : IAiProvider
    {
        private readonly Func<AiCompletionResult> _result;
        private readonly Exception? _throwOnce;
        private int _calls;

        public StubProvider(Func<AiCompletionResult> result, Exception? throwOnce = null)
        {
            _result = result;
            _throwOnce = throwOnce;
        }

        public string Name => "Stub";
        public int Calls => _calls;

        public Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken)
        {
            _calls++;
            if (_throwOnce is not null && _calls == 1)
            {
                var ex = _throwOnce;
                _throwOnce = null;
                throw ex;
            }
            return Task.FromResult(_result());
        }
    }

    private sealed class RecordingEvidenceSink : IEvidenceSink
    {
        public readonly List<EvidenceRecord> Records = new();
        public void Record(EvidenceRecord record) => Records.Add(record);
    }

    [Fact]
    public void SucceedsOnFirstAttemptAndRecordsEvidence()
    {
        var expected = new AiCompletionResult("{}", new AiUsage(10, 5, 15), "Stub", "test-model");
        var provider = new StubProvider(() => expected);
        var sink = new RecordingEvidenceSink();
        var service = new AiExecutionService(provider, sink, timeout: TimeSpan.FromSeconds(5), delay: _ => { });

        var outcome = service.Execute(new AiPromptRequest("PCT-EXE-0026", "sys", "user"), CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal(1, outcome.AttemptCount);
        Assert.Same(expected, outcome.Result);
        Assert.Single(sink.Records);
        Assert.Equal("Completed", sink.Records[0].Decision);
        Assert.Equal("PCT-EXE-0026", sink.Records[0].SubjectId);
    }

    [Fact]
    public void RetriesATransientFailureThenSucceeds()
    {
        var expected = new AiCompletionResult("{}", new AiUsage(1, 1, 2), "Stub", "test-model");
        var provider = new StubProvider(() => expected, throwOnce: new AiProviderException("rate limited", isTransient: true));
        var service = new AiExecutionService(provider, new RecordingEvidenceSink(), timeout: TimeSpan.FromSeconds(5), delay: _ => { });

        var outcome = service.Execute(new AiPromptRequest("PCT-EXE-0026", "sys", "user"), CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal(2, outcome.AttemptCount);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public void DoesNotRetryAStructuralFailure()
    {
        var provider = new StubProvider(
            () => throw new AiProviderException("bad request", isTransient: false),
            throwOnce: null);
        var service = new AiExecutionService(provider, new RecordingEvidenceSink(), timeout: TimeSpan.FromSeconds(5), delay: _ => { });

        var outcome = service.Execute(new AiPromptRequest("PCT-EXE-0026", "sys", "user"), CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal(1, outcome.AttemptCount);
        Assert.Contains("bad request", outcome.FailureReason);
    }

    [Fact]
    public void TimesOutAndReportsFailure()
    {
        var provider = new StubProvider(() =>
        {
            Thread.Sleep(200);
            return new AiCompletionResult("{}", new AiUsage(0, 0, 0), "Stub", "test-model");
        });
        var service = new AiExecutionService(provider, new RecordingEvidenceSink(), timeout: TimeSpan.FromMilliseconds(10), delay: _ => { });

        var outcome = service.Execute(new AiPromptRequest("PCT-EXE-0026", "sys", "user"), CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Contains("timed out", outcome.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HonorsCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var provider = new StubProvider(() => throw new OperationCanceledException());
        var service = new AiExecutionService(provider, new RecordingEvidenceSink(), timeout: TimeSpan.FromSeconds(5), delay: _ => { });

        var outcome = service.Execute(new AiPromptRequest("PCT-EXE-0026", "sys", "user"), cts.Token);

        Assert.False(outcome.Success);
        Assert.Contains("cancelled", outcome.FailureReason, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 4: Run to confirm it fails (types don't exist yet)**

Run: `dotnet test tests/Runtime.Ai.Tests/Runtime.Ai.Tests.csproj`
Expected: compile error, `AiExecutionService`/`AiExecutionOutcome` not found.

- [ ] **Step 5: Implement `AiExecutionOutcome`**

`src/Runtime.Ai/AiExecutionOutcome.cs`:
```csharp
namespace Runtime.Ai;

public sealed record AiExecutionOutcome
{
    public bool Success { get; }
    public AiCompletionResult? Result { get; }
    public string? FailureReason { get; }
    public TimeSpan Duration { get; }
    public int AttemptCount { get; }

    private AiExecutionOutcome(bool success, AiCompletionResult? result, string? failureReason, TimeSpan duration, int attemptCount)
    {
        Success = success;
        Result = result;
        FailureReason = failureReason;
        Duration = duration;
        AttemptCount = attemptCount;
    }

    public static AiExecutionOutcome Completed(AiCompletionResult result, TimeSpan duration, int attemptCount) =>
        new(true, result, null, duration, attemptCount);

    public static AiExecutionOutcome Failed(string failureReason, TimeSpan duration, int attemptCount) =>
        new(false, null, failureReason, duration, attemptCount);
}
```

- [ ] **Step 6: Implement `AiExecutionService`**

`src/Runtime.Ai/AiExecutionService.cs`:
```csharp
using System.Diagnostics;
using Kernel.Evidence;
using Kernel.Resilience;

namespace Runtime.Ai;

// The GA-1 orchestration point: wraps one IAiProvider call with the
// existing RetryExecutor's classification/backoff algorithm (no new
// resilience mechanism), a timeout via a linked CancellationTokenSource
// (no generic timeout wrapper existed anywhere in the Runtime before
// this - see Phase 0 Gap Analysis item "AI Execution Service"), and
// records one EvidenceRecord per call via IEvidenceSink, mirroring
// SkillHost.Invoke's own Contract/Decision/SubjectId/Reason/Timestamp
// shape exactly.
public sealed class AiExecutionService
{
    private readonly IAiProvider _provider;
    private readonly IEvidenceSink _evidenceSink;
    private readonly TimeSpan _timeout;
    private readonly RetryExecutor _retryExecutor;

    public AiExecutionService(
        IAiProvider provider,
        IEvidenceSink? evidenceSink = null,
        TimeSpan? timeout = null,
        Action<TimeSpan>? delay = null)
    {
        _provider = provider;
        _evidenceSink = evidenceSink ?? new InMemoryEvidenceSink();
        _timeout = timeout ?? TimeSpan.FromSeconds(60);
        _retryExecutor = new RetryExecutor(delay: delay);
    }

    public AiExecutionOutcome Execute(AiPromptRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var retryOutcome = _retryExecutor.Execute(() =>
        {
            using var timeoutCts = new CancellationTokenSource(_timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            try
            {
                var result = _provider.CompleteAsync(request, linkedCts.Token).GetAwaiter().GetResult();
                return AttemptResult.Success(result);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                return AttemptResult.Failure(FailureClassification.Transient, $"AI provider call timed out after {_timeout}.");
            }
            catch (OperationCanceledException)
            {
                return AttemptResult.Failure(FailureClassification.Structural, "AI provider call was cancelled by the caller.");
            }
            catch (AiProviderException ex)
            {
                return AttemptResult.Failure(ex.IsTransient ? FailureClassification.Transient : FailureClassification.Structural, ex.Message);
            }
        });

        stopwatch.Stop();

        _evidenceSink.Record(new EvidenceRecord(
            Contract: "AiExecutionService",
            Decision: retryOutcome.Succeeded ? "Completed" : "Failed",
            SubjectId: request.PromptContractId,
            Reason: retryOutcome.Succeeded ? null : retryOutcome.TerminalFailureReason,
            Timestamp: DateTimeOffset.UtcNow));

        return retryOutcome.Succeeded
            ? AiExecutionOutcome.Completed((AiCompletionResult)retryOutcome.Value!, stopwatch.Elapsed, retryOutcome.Attempts.Count)
            : AiExecutionOutcome.Failed(retryOutcome.TerminalFailureReason!, stopwatch.Elapsed, retryOutcome.Attempts.Count);
    }
}
```

Note: `RetryExecutor`'s constructor takes `(Func<int,TimeSpan>? backoff, Action<TimeSpan>? delay)` — confirm the moved copy still exposes both optional parameters; pass `delay` through by name so tests can zero out real sleeps (as the existing `Runtime.Tests` retry tests already do).

- [ ] **Step 7: Run to confirm green**

Run: `dotnet test tests/Runtime.Ai.Tests/Runtime.Ai.Tests.csproj`
Expected: all 6 tests pass.

- [ ] **Step 8: Run the full solution suite (resilience-type move touches shared code)**

Run: `dotnet test AefPlatform.sln`
Expected: 324 pre-existing + new Runtime.Ai.Tests all green, 0 failures.

- [ ] **Step 9: Commit**

```bash
git add src/Kernel/Resilience src/Runtime/Orchestration src/Runtime.Ai tests/Runtime.Ai.Tests tests/Runtime.Tests
git commit -m "feat(ai): add AiExecutionService (retry/timeout/cancellation/diagnostics), relocate resilience types to Kernel"
```

---

## Task 3: Prompt Execution Engine — template, renderer, structured response parser

**Files:**
- Create: `src/Runtime.Ai/Prompts/PromptTemplate.cs`
- Create: `src/Runtime.Ai/Prompts/PromptRenderResult.cs`
- Create: `src/Runtime.Ai/Prompts/PromptRenderer.cs`
- Create: `src/Runtime.Ai/StructuredResponseParser.cs`
- Test: `tests/Runtime.Ai.Tests/PromptRendererTests.cs`, `tests/Runtime.Ai.Tests/StructuredResponseParserTests.cs`

**Interfaces:**
- Produces: `PromptTemplate(string Id, string SystemPrompt, string UserPromptTemplate)`.
- Produces: `PromptRenderer.Render(PromptTemplate template, IReadOnlyDictionary<string, object?> variables) : PromptRenderResult`.
- Produces: `PromptRenderResult` with `bool Success`, `string? RenderedUserPrompt`, `IReadOnlyList<string> MissingVariables`.
- Produces: `StructuredResponseParser.TryParse<T>(string rawText, out T? value, out IReadOnlyList<string> errors) : bool` where `T` is a JSON-deserializable record.

- [ ] **Step 1: Write failing renderer tests**

`tests/Runtime.Ai.Tests/PromptRendererTests.cs`:
```csharp
using Runtime.Ai.Prompts;
using Xunit;

namespace Runtime.Ai.Tests;

public class PromptRendererTests
{
    [Fact]
    public void SubstitutesAllPlaceholders()
    {
        var template = new PromptTemplate("PCT-EXE-0026", "system", "Review {{specification_instance}} at iteration {{review_iteration}}.");
        var variables = new Dictionary<string, object?> { ["specification_instance"] = "SPEC-1", ["review_iteration"] = 1 };

        var result = PromptRenderer.Render(template, variables);

        Assert.True(result.Success);
        Assert.Equal("Review SPEC-1 at iteration 1.", result.RenderedUserPrompt);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void ReportsMissingVariablesInsteadOfLeavingRawPlaceholders()
    {
        var template = new PromptTemplate("PCT-EXE-0026", "system", "Review {{specification_instance}} at iteration {{review_iteration}}.");
        var variables = new Dictionary<string, object?> { ["specification_instance"] = "SPEC-1" };

        var result = PromptRenderer.Render(template, variables);

        Assert.False(result.Success);
        Assert.Null(result.RenderedUserPrompt);
        Assert.Equal(new[] { "review_iteration" }, result.MissingVariables);
    }
}
```

- [ ] **Step 2: Write failing structured-parser tests**

`tests/Runtime.Ai.Tests/StructuredResponseParserTests.cs`:
```csharp
using Runtime.Ai;
using Xunit;

namespace Runtime.Ai.Tests;

public class StructuredResponseParserTests
{
    private sealed record SampleOutput(string Status, int Score);

    [Fact]
    public void ParsesPlainJson()
    {
        var ok = StructuredResponseParser.TryParse<SampleOutput>("{\"Status\":\"ok\",\"Score\":7}", out var value, out var errors);

        Assert.True(ok);
        Assert.NotNull(value);
        Assert.Equal("ok", value!.Status);
        Assert.Equal(7, value.Score);
        Assert.Empty(errors);
    }

    [Fact]
    public void StripsMarkdownCodeFenceBeforeParsing()
    {
        var raw = "Here is the result:\n```json\n{\"Status\":\"ok\",\"Score\":9}\n```\n";

        var ok = StructuredResponseParser.TryParse<SampleOutput>(raw, out var value, out var errors);

        Assert.True(ok);
        Assert.Equal(9, value!.Score);
        Assert.Empty(errors);
    }

    [Fact]
    public void ReturnsErrorsInsteadOfThrowingOnMalformedJson()
    {
        var ok = StructuredResponseParser.TryParse<SampleOutput>("not json at all", out var value, out var errors);

        Assert.False(ok);
        Assert.Null(value);
        Assert.NotEmpty(errors);
    }
}
```

- [ ] **Step 3: Run to confirm both fail (types don't exist)**

Run: `dotnet test tests/Runtime.Ai.Tests/Runtime.Ai.Tests.csproj`
Expected: compile errors.

- [ ] **Step 4: Implement `PromptTemplate`/`PromptRenderResult`/`PromptRenderer`**

`src/Runtime.Ai/Prompts/PromptTemplate.cs`:
```csharp
namespace Runtime.Ai.Prompts;

public sealed record PromptTemplate(string Id, string SystemPrompt, string UserPromptTemplate);
```

`src/Runtime.Ai/Prompts/PromptRenderResult.cs`:
```csharp
namespace Runtime.Ai.Prompts;

public sealed record PromptRenderResult
{
    public bool Success { get; }
    public string? RenderedUserPrompt { get; }
    public IReadOnlyList<string> MissingVariables { get; }

    private PromptRenderResult(bool success, string? renderedUserPrompt, IReadOnlyList<string> missingVariables)
    {
        Success = success;
        RenderedUserPrompt = renderedUserPrompt;
        MissingVariables = missingVariables;
    }

    public static PromptRenderResult Rendered(string text) => new(true, text, Array.Empty<string>());

    public static PromptRenderResult MissingVariablesResult(IReadOnlyList<string> missing) => new(false, null, missing);
}
```

`src/Runtime.Ai/Prompts/PromptRenderer.cs`:
```csharp
using System.Text.RegularExpressions;

namespace Runtime.Ai.Prompts;

// Never leaves a raw {{placeholder}} in a rendered prompt sent to a real
// provider (that would silently corrupt the call) - a missing variable
// is reported back to the caller instead, same "escalate, never
// fabricate" discipline every existing Behavior in this Runtime follows.
public static class PromptRenderer
{
    private static readonly Regex PlaceholderPattern = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    public static PromptRenderResult Render(PromptTemplate template, IReadOnlyDictionary<string, object?> variables)
    {
        var missing = new List<string>();
        var rendered = PlaceholderPattern.Replace(template.UserPromptTemplate, match =>
        {
            var key = match.Groups[1].Value;
            if (variables.TryGetValue(key, out var value) && value is not null)
            {
                return value.ToString() ?? string.Empty;
            }

            missing.Add(key);
            return match.Value;
        });

        return missing.Count > 0
            ? PromptRenderResult.MissingVariablesResult(missing)
            : PromptRenderResult.Rendered(rendered);
    }
}
```

- [ ] **Step 5: Implement `StructuredResponseParser`**

`src/Runtime.Ai/StructuredResponseParser.cs`:
```csharp
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Runtime.Ai;

// Hand-rolled, matching the repo's existing no-third-party-library
// convention (Phase 0 Gap Analysis: no JSON-schema package referenced
// anywhere). "Schema validation" here means: valid JSON, deserializes
// into the caller's strongly-typed record (System.Text.Json already
// enforces required/typed shape for a `record` with no default
// constructor), never a generic untyped schema engine.
public static class StructuredResponseParser
{
    private static readonly Regex CodeFencePattern = new(@"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static bool TryParse<T>(string rawText, out T? value, out IReadOnlyList<string> errors) where T : class
    {
        var fenceMatch = CodeFencePattern.Match(rawText);
        var candidate = fenceMatch.Success ? fenceMatch.Groups[1].Value : rawText;

        try
        {
            value = JsonSerializer.Deserialize<T>(candidate, Options);
            if (value is null)
            {
                errors = new[] { "Response parsed to a null value." };
                return false;
            }

            errors = Array.Empty<string>();
            return true;
        }
        catch (JsonException ex)
        {
            value = null;
            errors = new[] { $"Response was not valid JSON: {ex.Message}" };
            return false;
        }
    }
}
```

- [ ] **Step 6: Run to confirm green**

Run: `dotnet test tests/Runtime.Ai.Tests/Runtime.Ai.Tests.csproj`
Expected: all tests pass (11 total so far).

- [ ] **Step 7: Commit**

```bash
git add src/Runtime.Ai/Prompts src/Runtime.Ai/StructuredResponseParser.cs tests/Runtime.Ai.Tests
git commit -m "feat(ai): add prompt template renderer and structured response parser"
```

---

## Task 4: Prompt catalog for `PCT-EXE-0026/0027/0028/0029`

**Files:**
- Create: `src/Runtime.Ai/Prompts/IPromptCatalog.cs`
- Create: `src/Runtime.Ai/Prompts/EngineeringPromptCatalog.cs`
- Test: `tests/Runtime.Ai.Tests/Prompts/EngineeringPromptCatalogTests.cs`

**Interfaces:**
- Produces: `IPromptCatalog.Get(string promptContractId) : PromptTemplate?` — consumed by Task 5/6 behaviors.
- Produces: `EngineeringPromptCatalog : IPromptCatalog`, parameterless constructor, pre-seeded with all four templates.

The four templates below render the Input/Output/Preconditions/Postconditions text already frozen in `ai-engineering-framework`'s governed `PCT-EXE-0026/0027/0028/0029` (read verbatim from that repo 2026-07-08) — this is not new prompt content, it is that content made executable.

- [ ] **Step 1: Write the failing catalog test**

`tests/Runtime.Ai.Tests/Prompts/EngineeringPromptCatalogTests.cs`:
```csharp
using Runtime.Ai.Prompts;
using Xunit;

namespace Runtime.Ai.Tests.Prompts;

public class EngineeringPromptCatalogTests
{
    [Theory]
    [InlineData("PCT-EXE-0026")]
    [InlineData("PCT-EXE-0027")]
    [InlineData("PCT-EXE-0028")]
    [InlineData("PCT-EXE-0029")]
    public void EveryGovernedPromptContractIsPresent(string id)
    {
        var catalog = new EngineeringPromptCatalog();

        var template = catalog.Get(id);

        Assert.NotNull(template);
        Assert.Equal(id, template!.Id);
        Assert.False(string.IsNullOrWhiteSpace(template.SystemPrompt));
        Assert.False(string.IsNullOrWhiteSpace(template.UserPromptTemplate));
    }

    [Fact]
    public void ReturnsNullForAnUnknownId()
    {
        var catalog = new EngineeringPromptCatalog();

        Assert.Null(catalog.Get("PCT-EXE-9999"));
    }
}
```

- [ ] **Step 2: Run to confirm it fails**

Run: `dotnet test tests/Runtime.Ai.Tests/Runtime.Ai.Tests.csproj`
Expected: compile error, `IPromptCatalog`/`EngineeringPromptCatalog` not found.

- [ ] **Step 3: Implement `IPromptCatalog`**

`src/Runtime.Ai/Prompts/IPromptCatalog.cs`:
```csharp
namespace Runtime.Ai.Prompts;

public interface IPromptCatalog
{
    PromptTemplate? Get(string promptContractId);
}
```

- [ ] **Step 4: Implement `EngineeringPromptCatalog`**

`src/Runtime.Ai/Prompts/EngineeringPromptCatalog.cs`:
```csharp
namespace Runtime.Ai.Prompts;

public sealed class EngineeringPromptCatalog : IPromptCatalog
{
    private static readonly IReadOnlyDictionary<string, PromptTemplate> Templates = new Dictionary<string, PromptTemplate>
    {
        ["PCT-EXE-0026"] = new PromptTemplate(
            "PCT-EXE-0026",
            "You are the Engineering Specification Reviewer (AGT-EXE-0013). You independently review a Business-Approved Engineering Specification. You never modify it. You evaluate eight areas: Business Goal clarity and measurability; Existing Capability Analysis; Functional Requirements; Non-Functional Requirements; Architecture; Engineering Quality; Governance compliance; Final Recommendation. Respond with ONLY a single JSON object matching this shape, no prose outside it: {\"review_instance\":{\"review_target\":string,\"review_iteration\":number,\"findings\":[{\"area\":string,\"severity\":\"Critical\"|\"Major\"|\"Minor\"|\"Optional\",\"reason\":string,\"evidence\":string,\"recommendation\":string}]},\"final_recommendation\":\"Approved\"|\"Approved with Comments\"|\"Requires Revision\"|\"Rejected\",\"status\":\"review_complete\"|\"terminal_failure\",\"failure_reason\":string|null}. Use status=\"terminal_failure\" ONLY for a structural failure that prevents evaluation (e.g. the specification is unreadable or fundamentally non-conformant to its schema) - never for engineering disagreement or poor quality, which belong in final_recommendation instead.",
            "specification_instance:\n{{specification_instance}}\n\nreview_iteration: {{review_iteration}}"),

        ["PCT-EXE-0027"] = new PromptTemplate(
            "PCT-EXE-0027",
            "You are realizing SKL-EXE-0027 (Specification Conformance Validation) for the Validation Agent (AGT-EXE-0014). Given an implementation_log and test_plan_and_results, and a specification_instance where one exists (if null, validate against the unit of work's own Plan/Root Cause Analysis content supplied in its place), determine conformance for exactly these areas: Functional Requirements, Tests, Traceability. You never re-execute a test - you audit test_plan_and_results' existing content only. Respond with ONLY a single JSON object: {\"conformance_findings\":[{\"area\":\"Functional Requirements\"|\"Tests\"|\"Traceability\",\"classification\":\"PASS\"|\"WARNING\"|\"FAIL\"|\"UNKNOWN\",\"rationale\":string}],\"uncovered_specification_sections\":[string]}. Name every specification section with no corresponding implementation trace in uncovered_specification_sections - never omit one silently. Use classification=\"UNKNOWN\" with a non-empty rationale when no resolvable check exists - never fabricate a PASS.",
            "specification_instance:\n{{specification_instance}}\n\nimplementation_log:\n{{implementation_log}}\n\ntest_plan_and_results:\n{{test_plan_and_results}}"),

        ["PCT-EXE-0028"] = new PromptTemplate(
            "PCT-EXE-0028",
            "You are realizing SKL-EXE-0028 (Architecture, Standards and NFR Conformance Review) for the Validation Agent (AGT-EXE-0014). Given a technology_profile, documentation, and a specification_instance where one exists, review architecture, repository standards, coding standards, security, performance, scalability, dependencies, backward compatibility, configuration, and operational readiness. You never author documentation prose - you only review what documentation already contains. Respond with ONLY a single JSON object: {\"conformance_findings\":[{\"area\":string,\"classification\":\"PASS\"|\"WARNING\"|\"FAIL\"|\"UNKNOWN\",\"rationale\":string}]}. Every area you were asked to cover must appear exactly once. classification=\"UNKNOWN\" always carries a non-empty rationale - never silently passed.",
            "technology_profile:\n{{technology_profile}}\n\ndocumentation:\n{{documentation}}\n\nspecification_instance:\n{{specification_instance}}"),

        ["PCT-EXE-0029"] = new PromptTemplate(
            "PCT-EXE-0029",
            "You are realizing SKL-EXE-0029 (Validation Findings Synthesis and Merge-Readiness Reporting) for the Validation Agent (AGT-EXE-0014). Given the combined conformance_findings from SKL-EXE-0027 and SKL-EXE-0028 (covering all 14 Validation Areas), synthesize one REV instance (Review Target: \"Implementation Validation\") and a merge_readiness_signal. You never render the merge decision yourself - merge_readiness_signal is evidence for a human Review gate to consume. Respond with ONLY a single JSON object: {\"review_instance\":{\"review_target\":\"Implementation Validation\",\"findings\":[{\"area\":string,\"severity\":\"Critical\"|\"Major\"|\"Minor\"|\"Optional\",\"reason\":string,\"evidence\":string,\"recommendation\":string}]},\"merge_readiness_signal\":\"ready\"|\"not_ready_human_approval_required\"}. Return \"not_ready_human_approval_required\" whenever any finding is an unresolved Critical, or a Major with no recorded acceptance rationale - never silently \"ready\".",
            "conformance_findings:\n{{conformance_findings}}"),
    };

    public PromptTemplate? Get(string promptContractId) => Templates.TryGetValue(promptContractId, out var template) ? template : null;
}
```

- [ ] **Step 5: Run to confirm green**

Run: `dotnet test tests/Runtime.Ai.Tests/Runtime.Ai.Tests.csproj`

- [ ] **Step 6: Commit**

```bash
git add src/Runtime.Ai/Prompts tests/Runtime.Ai.Tests/Prompts
git commit -m "feat(ai): seed prompt catalog with PCT-EXE-0026..0029 templates"
```

---

## Task 5: OpenAI provider

**Files:**
- Create: `src/Runtime.Ai/Providers/OpenAiProviderOptions.cs`
- Create: `src/Runtime.Ai/Providers/OpenAiProvider.cs`
- Test: `tests/Runtime.Ai.Tests/Providers/OpenAiProviderTests.cs`

**Interfaces:**
- Consumes: `IAiProvider` (Task 1).
- Produces: `OpenAiProvider : IAiProvider`, constructor `(HttpClient httpClient, OpenAiProviderOptions options)`.
- Produces: `OpenAiProviderOptions { string ApiKey; string Model; string BaseUrl = "https://api.openai.com/v1"; }`.

- [ ] **Step 1: Write the failing test using a fake `HttpMessageHandler`**

`tests/Runtime.Ai.Tests/Providers/OpenAiProviderTests.cs`:
```csharp
using System.Net;
using System.Text;
using Runtime.Ai;
using Runtime.Ai.Providers;
using Xunit;

namespace Runtime.Ai.Tests.Providers;

public class OpenAiProviderTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public HttpRequestMessage? LastRequest;
        public string? LastRequestBody;

        public FakeHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly string SuccessBody = """
    {
      "model": "gpt-4o",
      "choices": [ { "message": { "content": "{\"status\":\"ok\"}" } } ],
      "usage": { "prompt_tokens": 12, "completion_tokens": 4, "total_tokens": 16 }
    }
    """;

    [Fact]
    public async Task SendsBearerAuthAndReturnsParsedUsage()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, SuccessBody);
        var httpClient = new HttpClient(handler);
        var provider = new OpenAiProvider(httpClient, new OpenAiProviderOptions { ApiKey = "sk-test", Model = "gpt-4o" });

        var result = await provider.CompleteAsync(new AiPromptRequest("PCT-EXE-0026", "system", "user"), CancellationToken.None);

        Assert.Equal("{\"status\":\"ok\"}", result.RawText);
        Assert.Equal(16, result.Usage.TotalTokens);
        Assert.Equal("OpenAI", result.ProviderName);
        Assert.Equal("sk-test", handler.LastRequest!.Headers.Authorization!.Parameter);
        Assert.Contains("\"model\":\"gpt-4o\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task ClassifiesA429AsTransient()
    {
        var handler = new FakeHandler(HttpStatusCode.TooManyRequests, "{\"error\":\"rate limited\"}");
        var provider = new OpenAiProvider(new HttpClient(handler), new OpenAiProviderOptions { ApiKey = "sk-test", Model = "gpt-4o" });

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.CompleteAsync(new AiPromptRequest("PCT-EXE-0026", "system", "user"), CancellationToken.None));

        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task ClassifiesA401AsStructural()
    {
        var handler = new FakeHandler(HttpStatusCode.Unauthorized, "{\"error\":\"bad key\"}");
        var provider = new OpenAiProvider(new HttpClient(handler), new OpenAiProviderOptions { ApiKey = "sk-bad", Model = "gpt-4o" });

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.CompleteAsync(new AiPromptRequest("PCT-EXE-0026", "system", "user"), CancellationToken.None));

        Assert.False(ex.IsTransient);
    }
}
```

- [ ] **Step 2: Run to confirm it fails to compile**

Run: `dotnet test tests/Runtime.Ai.Tests/Runtime.Ai.Tests.csproj`

- [ ] **Step 3: Implement `OpenAiProviderOptions`**

`src/Runtime.Ai/Providers/OpenAiProviderOptions.cs`:
```csharp
namespace Runtime.Ai.Providers;

public sealed class OpenAiProviderOptions
{
    public required string ApiKey { get; init; }
    public required string Model { get; init; }
    public string BaseUrl { get; init; } = "https://api.openai.com/v1";
}
```

- [ ] **Step 4: Implement `OpenAiProvider`**

`src/Runtime.Ai/Providers/OpenAiProvider.cs`:
```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Runtime.Ai.Providers;

public sealed class OpenAiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiProviderOptions _options;

    public OpenAiProvider(HttpClient httpClient, OpenAiProviderOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public string Name => "OpenAI";

    public async Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Content = JsonContent.Create(new
        {
            model = _options.Model,
            temperature = request.Temperature,
            max_tokens = request.MaxOutputTokens,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt },
            },
        });

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException($"OpenAI request failed: {ex.Message}", isTransient: true, ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var isTransient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            throw new AiProviderException($"OpenAI returned {(int)response.StatusCode}: {body}", isTransient);
        }

        OpenAiChatCompletionResponse parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(body)
                ?? throw new AiProviderException("OpenAI returned an empty response body.", isTransient: false);
        }
        catch (JsonException ex)
        {
            throw new AiProviderException($"OpenAI returned an unparseable response: {ex.Message}", isTransient: false, ex);
        }

        var content = parsed.Choices.FirstOrDefault()?.Message?.Content
            ?? throw new AiProviderException("OpenAI response carried no choices/message content.", isTransient: false);

        return new AiCompletionResult(
            content,
            new AiUsage(parsed.Usage?.PromptTokens ?? 0, parsed.Usage?.CompletionTokens ?? 0, parsed.Usage?.TotalTokens ?? 0),
            Name,
            parsed.Model ?? _options.Model);
    }

    private sealed record OpenAiChatCompletionResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] List<OpenAiChoice> Choices,
        [property: JsonPropertyName("usage")] OpenAiUsage? Usage);

    private sealed record OpenAiChoice([property: JsonPropertyName("message")] OpenAiMessage? Message);

    private sealed record OpenAiMessage([property: JsonPropertyName("content")] string? Content);

    private sealed record OpenAiUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens,
        [property: JsonPropertyName("total_tokens")] int TotalTokens);
}
```

- [ ] **Step 5: Run to confirm green**

Run: `dotnet test tests/Runtime.Ai.Tests/Runtime.Ai.Tests.csproj`

- [ ] **Step 6: Commit**

```bash
git add src/Runtime.Ai/Providers/OpenAi* tests/Runtime.Ai.Tests/Providers/OpenAiProviderTests.cs
git commit -m "feat(ai): implement OpenAI provider over HttpClient"
```

---

## Task 6: Azure OpenAI provider

**Files:**
- Create: `src/Runtime.Ai/Providers/AzureOpenAiProviderOptions.cs`
- Create: `src/Runtime.Ai/Providers/AzureOpenAiProvider.cs`
- Test: `tests/Runtime.Ai.Tests/Providers/AzureOpenAiProviderTests.cs`

**Interfaces:**
- Consumes: `IAiProvider` (Task 1). Same request/result shape as `OpenAiProvider` — the two are interchangeable behind `IAiProvider` with zero caller-side branching (the point of the abstraction).
- Produces: `AzureOpenAiProvider : IAiProvider`, constructor `(HttpClient httpClient, AzureOpenAiProviderOptions options)`.
- Produces: `AzureOpenAiProviderOptions { string Endpoint; string ApiKey; string Deployment; string ApiVersion = "2024-06-01"; }`.

- [ ] **Step 1: Write the failing test**

`tests/Runtime.Ai.Tests/Providers/AzureOpenAiProviderTests.cs`:
```csharp
using System.Net;
using System.Text;
using Runtime.Ai;
using Runtime.Ai.Providers;
using Xunit;

namespace Runtime.Ai.Tests.Providers;

public class AzureOpenAiProviderTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public HttpRequestMessage? LastRequest;

        public FakeHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") });
        }
    }

    private static readonly string SuccessBody = """
    {
      "model": "gpt-4o",
      "choices": [ { "message": { "content": "{\"status\":\"ok\"}" } } ],
      "usage": { "prompt_tokens": 20, "completion_tokens": 6, "total_tokens": 26 }
    }
    """;

    [Fact]
    public async Task PostsToTheDeploymentSpecificUrlWithApiKeyHeader()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, SuccessBody);
        var options = new AzureOpenAiProviderOptions
        {
            Endpoint = "https://example.openai.azure.com",
            ApiKey = "azure-key",
            Deployment = "gpt-4o-deployment",
        };
        var provider = new AzureOpenAiProvider(new HttpClient(handler), options);

        var result = await provider.CompleteAsync(new AiPromptRequest("PCT-EXE-0027", "system", "user"), CancellationToken.None);

        Assert.Equal("{\"status\":\"ok\"}", result.RawText);
        Assert.Equal(26, result.Usage.TotalTokens);
        Assert.Equal("AzureOpenAI", result.ProviderName);
        Assert.Equal(
            "https://example.openai.azure.com/openai/deployments/gpt-4o-deployment/chat/completions?api-version=2024-06-01",
            handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("azure-key", handler.LastRequest.Headers.GetValues("api-key").Single());
    }

    [Fact]
    public async Task ClassifiesA503AsTransient()
    {
        var handler = new FakeHandler(HttpStatusCode.ServiceUnavailable, "{\"error\":\"unavailable\"}");
        var provider = new AzureOpenAiProvider(new HttpClient(handler), new AzureOpenAiProviderOptions
        {
            Endpoint = "https://example.openai.azure.com",
            ApiKey = "azure-key",
            Deployment = "gpt-4o-deployment",
        });

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.CompleteAsync(new AiPromptRequest("PCT-EXE-0027", "system", "user"), CancellationToken.None));

        Assert.True(ex.IsTransient);
    }
}
```

- [ ] **Step 2: Run to confirm it fails to compile**

- [ ] **Step 3: Implement `AzureOpenAiProviderOptions`**

`src/Runtime.Ai/Providers/AzureOpenAiProviderOptions.cs`:
```csharp
namespace Runtime.Ai.Providers;

public sealed class AzureOpenAiProviderOptions
{
    public required string Endpoint { get; init; }
    public required string ApiKey { get; init; }
    public required string Deployment { get; init; }
    public string ApiVersion { get; init; } = "2024-06-01";
}
```

- [ ] **Step 4: Implement `AzureOpenAiProvider`**

`src/Runtime.Ai/Providers/AzureOpenAiProvider.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Runtime.Ai.Providers;

public sealed class AzureOpenAiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly AzureOpenAiProviderOptions _options;

    public AzureOpenAiProvider(HttpClient httpClient, AzureOpenAiProviderOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public string Name => "AzureOpenAI";

    public async Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken)
    {
        var url = $"{_options.Endpoint.TrimEnd('/')}/openai/deployments/{_options.Deployment}/chat/completions?api-version={_options.ApiVersion}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Add("api-key", _options.ApiKey);
        httpRequest.Content = JsonContent.Create(new
        {
            temperature = request.Temperature,
            max_tokens = request.MaxOutputTokens,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt },
            },
        });

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException($"Azure OpenAI request failed: {ex.Message}", isTransient: true, ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var isTransient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            throw new AiProviderException($"Azure OpenAI returned {(int)response.StatusCode}: {body}", isTransient);
        }

        AzureChatCompletionResponse parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AzureChatCompletionResponse>(body)
                ?? throw new AiProviderException("Azure OpenAI returned an empty response body.", isTransient: false);
        }
        catch (JsonException ex)
        {
            throw new AiProviderException($"Azure OpenAI returned an unparseable response: {ex.Message}", isTransient: false, ex);
        }

        var content = parsed.Choices.FirstOrDefault()?.Message?.Content
            ?? throw new AiProviderException("Azure OpenAI response carried no choices/message content.", isTransient: false);

        return new AiCompletionResult(
            content,
            new AiUsage(parsed.Usage?.PromptTokens ?? 0, parsed.Usage?.CompletionTokens ?? 0, parsed.Usage?.TotalTokens ?? 0),
            Name,
            parsed.Model ?? _options.Deployment);
    }

    private sealed record AzureChatCompletionResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] List<AzureChoice> Choices,
        [property: JsonPropertyName("usage")] AzureUsage? Usage);

    private sealed record AzureChoice([property: JsonPropertyName("message")] AzureMessage? Message);

    private sealed record AzureMessage([property: JsonPropertyName("content")] string? Content);

    private sealed record AzureUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens,
        [property: JsonPropertyName("total_tokens")] int TotalTokens);
}
```

- [ ] **Step 5: Run to confirm green, then run the full `Runtime.Ai.Tests` project**

- [ ] **Step 6: Commit**

```bash
git add src/Runtime.Ai/Providers/AzureOpenAi* tests/Runtime.Ai.Tests/Providers/AzureOpenAiProviderTests.cs
git commit -m "feat(ai): implement Azure OpenAI provider over HttpClient"
```

---

## Task 7: `AGT-EXE-0013` — `SpecificationIndependentReviewBehavior` (`SKL-EXE-0026`)

**Files:**
- Create: `src/Runtime/DomainContent/EngineeringSpecificationReview/EngineeringSpecificationReviewTypes.cs`
- Create: `src/Runtime/DomainContent/EngineeringSpecificationReview/SpecificationIndependentReviewBehavior.cs`
- Modify: `src/Runtime/Runtime.csproj` (add `ProjectReference` to `Runtime.Ai`)
- Test: `tests/Runtime.Tests/DomainContent/EngineeringSpecificationReview/SpecificationIndependentReviewBehaviorTests.cs`

**Interfaces:**
- Consumes: `AiExecutionService.Execute` (Task 2), `IPromptCatalog.Get` (Task 4), `StructuredResponseParser.TryParse<T>` (Task 3), `IBehavior` (existing, `object? Execute(RuntimeExecutionContext context)`).
- Produces: `SpecificationReviewRequest(object SpecificationInstance, int ReviewIteration)` (the `Tier3SkillContent["SpecificationReviewRequest"]` key), `EngineeringReviewInstance(string ReviewTarget, int ReviewIteration, IReadOnlyList<EngineeringReviewFinding> Findings)`, `EngineeringReviewFinding(string Area, string Severity, string Reason, string Evidence, string Recommendation)`, `SpecificationReviewOutcome` with `bool IsEscalated`, `string? EscalationReason`, `EngineeringReviewInstance? ReviewInstance`, `string? FinalRecommendation` — consumed by Task 9's pipeline wiring.

- [ ] **Step 1: Add the project reference**

`src/Runtime/Runtime.csproj` — add inside the existing `<ItemGroup>` with the `Kernel` reference:
```xml
<ProjectReference Include="..\Runtime.Ai\Runtime.Ai.csproj" />
```

- [ ] **Step 2: Write the failing test**

`tests/Runtime.Tests/DomainContent/EngineeringSpecificationReview/SpecificationIndependentReviewBehaviorTests.cs`:
```csharp
using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Ai.Providers;
using Runtime.Context;
using Runtime.DomainContent.EngineeringSpecificationReview;
using Xunit;

namespace Runtime.Tests.DomainContent.EngineeringSpecificationReview;

public class SpecificationIndependentReviewBehaviorTests
{
    private sealed class StubProvider : IAiProvider
    {
        private readonly string _rawText;
        public StubProvider(string rawText) => _rawText = rawText;
        public string Name => "Stub";
        public Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCompletionResult(_rawText, new AiUsage(1, 1, 2), "Stub", "test-model"));
    }

    private static RuntimeExecutionContext ContextWith(object request) =>
        new("test-instance", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Review",
            new Dictionary<string, object> { ["SpecificationReviewRequest"] = request });

    [Fact]
    public void ReturnsParsedReviewOnSuccess()
    {
        var raw = """
        {"review_instance":{"review_target":"SPEC-1","review_iteration":1,"findings":[{"area":"Business Goal","severity":"Minor","reason":"vague metric","evidence":"line 4","recommendation":"quantify"}]},"final_recommendation":"Approved with Comments","status":"review_complete","failure_reason":null}
        """;
        var service = new AiExecutionService(new StubProvider(raw), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new SpecificationIndependentReviewBehavior(service, new EngineeringPromptCatalog());

        var result = behavior.Execute(ContextWith(new SpecificationReviewRequest("SPEC-1 content", 1))) as SpecificationReviewOutcome;

        Assert.NotNull(result);
        Assert.False(result!.IsEscalated);
        Assert.Equal("Approved with Comments", result.FinalRecommendation);
        Assert.Single(result.ReviewInstance!.Findings);
    }

    [Fact]
    public void EscalatesOnMalformedAiResponse()
    {
        var service = new AiExecutionService(new StubProvider("not json"), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new SpecificationIndependentReviewBehavior(service, new EngineeringPromptCatalog());

        var result = behavior.Execute(ContextWith(new SpecificationReviewRequest("SPEC-1 content", 1))) as SpecificationReviewOutcome;

        Assert.NotNull(result);
        Assert.True(result!.IsEscalated);
        Assert.NotNull(result.EscalationReason);
    }

    [Fact]
    public void EscalatesWhenNoRequestIsSupplied()
    {
        var service = new AiExecutionService(new StubProvider("{}"), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new SpecificationIndependentReviewBehavior(service, new EngineeringPromptCatalog());
        var emptyContext = new RuntimeExecutionContext("x", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Review", new Dictionary<string, object>());

        var result = behavior.Execute(emptyContext) as SpecificationReviewOutcome;

        Assert.NotNull(result);
        Assert.True(result!.IsEscalated);
    }
}
```

- [ ] **Step 3: Run to confirm it fails to compile**

- [ ] **Step 4: Implement the types**

`src/Runtime/DomainContent/EngineeringSpecificationReview/EngineeringSpecificationReviewTypes.cs`:
```csharp
namespace Runtime.DomainContent.EngineeringSpecificationReview;

public sealed record SpecificationReviewRequest(object SpecificationInstance, int ReviewIteration);

public sealed record EngineeringReviewFinding(string Area, string Severity, string Reason, string Evidence, string Recommendation);

public sealed record EngineeringReviewInstance(string ReviewTarget, int ReviewIteration, IReadOnlyList<EngineeringReviewFinding> Findings);

public sealed record SpecificationReviewOutcome
{
    public bool IsEscalated { get; }
    public string? EscalationReason { get; }
    public EngineeringReviewInstance? ReviewInstance { get; }
    public string? FinalRecommendation { get; }

    private SpecificationReviewOutcome(bool isEscalated, string? escalationReason, EngineeringReviewInstance? reviewInstance, string? finalRecommendation)
    {
        IsEscalated = isEscalated;
        EscalationReason = escalationReason;
        ReviewInstance = reviewInstance;
        FinalRecommendation = finalRecommendation;
    }

    public static SpecificationReviewOutcome Completed(EngineeringReviewInstance reviewInstance, string finalRecommendation) =>
        new(false, null, reviewInstance, finalRecommendation);

    public static SpecificationReviewOutcome Escalated(string reason) => new(true, reason, null, null);
}

internal sealed record AiSpecificationReviewResponse(
    AiReviewInstanceDto? Review_Instance,
    string? Final_Recommendation,
    string? Status,
    string? Failure_Reason);

internal sealed record AiReviewInstanceDto(string Review_Target, int Review_Iteration, List<AiReviewFindingDto> Findings);

internal sealed record AiReviewFindingDto(string Area, string Severity, string Reason, string Evidence, string Recommendation);
```

- [ ] **Step 5: Implement the behavior**

`src/Runtime/DomainContent/EngineeringSpecificationReview/SpecificationIndependentReviewBehavior.cs`:
```csharp
using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.EngineeringSpecificationReview;

// Realizes SKL-EXE-0026 for AGT-EXE-0013 (PCT-EXE-0026). Per that
// Prompt Contract: status="terminal_failure" is reserved exclusively
// for structural failures preventing evaluation (schema non-conformance,
// unreadable input, an AI call that fails outright, or a response that
// cannot be parsed) - it is never used for an ordinary "Requires
// Revision"/"Rejected" outcome, which is carried in FinalRecommendation
// as an escalation-free, successful result.
public sealed class SpecificationIndependentReviewBehavior : IBehavior
{
    private const string PromptContractId = "PCT-EXE-0026";
    private readonly AiExecutionService _aiExecutionService;
    private readonly IPromptCatalog _promptCatalog;

    public SpecificationIndependentReviewBehavior(AiExecutionService aiExecutionService, IPromptCatalog promptCatalog)
    {
        _aiExecutionService = aiExecutionService;
        _promptCatalog = promptCatalog;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("SpecificationReviewRequest", out var raw) || raw is not SpecificationReviewRequest request)
        {
            return SpecificationReviewOutcome.Escalated("No SpecificationReviewRequest was supplied in the execution context.");
        }

        var template = _promptCatalog.Get(PromptContractId);
        if (template is null)
        {
            return SpecificationReviewOutcome.Escalated($"No prompt template registered for {PromptContractId}.");
        }

        var renderResult = PromptRenderer.Render(template, new Dictionary<string, object?>
        {
            ["specification_instance"] = request.SpecificationInstance,
            ["review_iteration"] = request.ReviewIteration,
        });

        if (!renderResult.Success)
        {
            return SpecificationReviewOutcome.Escalated($"Prompt rendering failed, missing: {string.Join(", ", renderResult.MissingVariables)}.");
        }

        var executionOutcome = _aiExecutionService.Execute(
            new AiPromptRequest(PromptContractId, template.SystemPrompt, renderResult.RenderedUserPrompt!),
            CancellationToken.None);

        if (!executionOutcome.Success)
        {
            return SpecificationReviewOutcome.Escalated(executionOutcome.FailureReason!);
        }

        if (!StructuredResponseParser.TryParse<AiSpecificationReviewResponse>(executionOutcome.Result!.RawText, out var parsed, out var errors))
        {
            return SpecificationReviewOutcome.Escalated($"AI response for {PromptContractId} could not be parsed: {string.Join("; ", errors)}.");
        }

        if (parsed!.Status == "terminal_failure")
        {
            return SpecificationReviewOutcome.Escalated(parsed.Failure_Reason ?? "AI reported a terminal_failure with no reason.");
        }

        if (parsed.Review_Instance is null || parsed.Final_Recommendation is null)
        {
            return SpecificationReviewOutcome.Escalated($"AI response for {PromptContractId} was missing review_instance or final_recommendation.");
        }

        var reviewInstance = new EngineeringReviewInstance(
            parsed.Review_Instance.Review_Target,
            parsed.Review_Instance.Review_Iteration,
            parsed.Review_Instance.Findings.Select(f => new EngineeringReviewFinding(f.Area, f.Severity, f.Reason, f.Evidence, f.Recommendation)).ToList());

        return SpecificationReviewOutcome.Completed(reviewInstance, parsed.Final_Recommendation);
    }
}
```

Note: `System.Text.Json`'s default `PropertyNameCaseInsensitive = true` (set in `StructuredResponseParser`) maps JSON `review_instance` to a C# property named `Review_Instance` only if the JSON key matches case-insensitively against the property name **as written** — `review_instance` vs `Review_Instance` differ only by casing per underscore-segment, which case-insensitive matching does handle. Verify this in Step 6; if `System.Text.Json` does not bridge snake_case to PascalCase automatically (it does not do snake_case-to-camelCase conversion, only case-insensitivity on identical-shape names), add `[JsonPropertyName("review_instance")]` etc. attributes to every property on `AiSpecificationReviewResponse`/`AiReviewInstanceDto`/`AiReviewFindingDto` instead of relying on case-insensitivity alone.

- [ ] **Step 6: Run to confirm green; fix JSON property mapping if Step 5's note applies**

Run: `dotnet test tests/Runtime.Tests/Runtime.Tests.csproj --filter SpecificationIndependentReviewBehaviorTests`
Expected: 3/3 pass. If the parse-based tests fail because snake_case JSON didn't bind, add explicit `[JsonPropertyName]` attributes (matching the snake_case names in the Step 4 DTOs) and re-run.

- [ ] **Step 7: Commit**

```bash
git add src/Runtime/Runtime.csproj src/Runtime/DomainContent/EngineeringSpecificationReview tests/Runtime.Tests/DomainContent/EngineeringSpecificationReview
git commit -m "feat(ai): implement AGT-EXE-0013 Specification Reviewer via SKL-EXE-0026"
```

---

## Task 8: `AGT-EXE-0014` — `SKL-EXE-0027/0028/0029` behaviors

**Files:**
- Create: `src/Runtime/DomainContent/ImplementationValidation/ImplementationValidationTypes.cs`
- Create: `src/Runtime/DomainContent/ImplementationValidation/SpecificationConformanceValidationBehavior.cs`
- Create: `src/Runtime/DomainContent/ImplementationValidation/ArchitectureStandardsConformanceReviewBehavior.cs`
- Create: `src/Runtime/DomainContent/ImplementationValidation/ValidationFindingsSynthesisBehavior.cs`
- Test: `tests/Runtime.Tests/DomainContent/ImplementationValidation/ImplementationValidationBehaviorTests.cs`

**Interfaces:**
- Produces: `ConformanceFinding(string Area, string Classification, string Rationale)` — shared output element type for both `SKL-EXE-0027` and `SKL-EXE-0028`, and the input element type for `SKL-EXE-0029` (matches `PCT-EXE-0029`'s "combined output of SKL-EXE-0027 and SKL-EXE-0028" contract).
- Produces: `SpecificationConformanceValidationRequest(object? SpecificationInstance, object ImplementationLog, object TestPlanAndResults)`, `SpecificationConformanceValidationResult(bool IsEscalated, string? EscalationReason, IReadOnlyList<ConformanceFinding>? ConformanceFindings, IReadOnlyList<string>? UncoveredSpecificationSections)`.
- Produces: `ArchitectureStandardsConformanceReviewRequest(object TechnologyProfile, object Documentation, object? SpecificationInstance)`, `ArchitectureStandardsConformanceReviewResult(bool IsEscalated, string? EscalationReason, IReadOnlyList<ConformanceFinding>? ConformanceFindings)`.
- Produces: `ValidationFindingsSynthesisRequest(IReadOnlyList<ConformanceFinding> ConformanceFindings)`, `ValidationFindingsSynthesisResult(bool IsEscalated, string? EscalationReason, EngineeringReviewInstance? ReviewInstance, string? MergeReadinessSignal)` (reuses `EngineeringReviewInstance`/`EngineeringReviewFinding` from Task 7 — same shape, same governed severity vocabulary per `AGT-EXE-0014`'s own "reusing SCH-DEL-0004 severity vocabulary verbatim" rule — add a `using Runtime.DomainContent.EngineeringSpecificationReview;` rather than redefining it).

- [ ] **Step 1: Write the failing tests for all three behaviors**

`tests/Runtime.Tests/DomainContent/ImplementationValidation/ImplementationValidationBehaviorTests.cs`:
```csharp
using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Context;
using Runtime.DomainContent.ImplementationValidation;
using Xunit;

namespace Runtime.Tests.DomainContent.ImplementationValidation;

public class ImplementationValidationBehaviorTests
{
    private sealed class StubProvider : IAiProvider
    {
        private readonly string _rawText;
        public StubProvider(string rawText) => _rawText = rawText;
        public string Name => "Stub";
        public Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCompletionResult(_rawText, new AiUsage(1, 1, 2), "Stub", "test-model"));
    }

    private static RuntimeExecutionContext ContextWith(string key, object request) =>
        new("test-instance", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Validation",
            new Dictionary<string, object> { [key] = request });

    [Fact]
    public void ConformanceValidationParsesFindingsAndUncoveredSections()
    {
        var raw = """
        {"conformance_findings":[{"area":"Tests","classification":"PASS","rationale":"covered"}],"uncovered_specification_sections":["NFR-3"]}
        """;
        var service = new AiExecutionService(new StubProvider(raw), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new SpecificationConformanceValidationBehavior(service, new EngineeringPromptCatalog());

        var result = behavior.Execute(ContextWith("SpecificationConformanceValidationRequest",
            new SpecificationConformanceValidationRequest(null, "log", "results"))) as SpecificationConformanceValidationResult;

        Assert.NotNull(result);
        Assert.False(result!.IsEscalated);
        Assert.Single(result.ConformanceFindings!);
        Assert.Equal(new[] { "NFR-3" }, result.UncoveredSpecificationSections);
    }

    [Fact]
    public void ArchitectureStandardsReviewParsesFindings()
    {
        var raw = """{"conformance_findings":[{"area":"Security","classification":"WARNING","rationale":"no rate limiting"}]}""";
        var service = new AiExecutionService(new StubProvider(raw), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new ArchitectureStandardsConformanceReviewBehavior(service, new EngineeringPromptCatalog());

        var result = behavior.Execute(ContextWith("ArchitectureStandardsConformanceReviewRequest",
            new ArchitectureStandardsConformanceReviewRequest("profile", "docs", null))) as ArchitectureStandardsConformanceReviewResult;

        Assert.NotNull(result);
        Assert.False(result!.IsEscalated);
        Assert.Single(result.ConformanceFindings!);
    }

    [Fact]
    public void SynthesisReturnsNotReadyWhenACriticalFindingIsPresent()
    {
        var raw = """
        {"review_instance":{"review_target":"Implementation Validation","review_iteration":1,"findings":[{"area":"Security","severity":"Critical","reason":"secret in code","evidence":"file.cs:10","recommendation":"remove"}]},"merge_readiness_signal":"not_ready_human_approval_required"}
        """;
        var service = new AiExecutionService(new StubProvider(raw), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new ValidationFindingsSynthesisBehavior(service, new EngineeringPromptCatalog());
        var findings = new[] { new ConformanceFinding("Security", "FAIL", "secret in code") };

        var result = behavior.Execute(ContextWith("ValidationFindingsSynthesisRequest",
            new ValidationFindingsSynthesisRequest(findings))) as ValidationFindingsSynthesisResult;

        Assert.NotNull(result);
        Assert.False(result!.IsEscalated);
        Assert.Equal("not_ready_human_approval_required", result.MergeReadinessSignal);
        Assert.Single(result.ReviewInstance!.Findings);
    }

    [Fact]
    public void EscalatesOnUnparseableAiResponse()
    {
        var service = new AiExecutionService(new StubProvider("not json"), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new SpecificationConformanceValidationBehavior(service, new EngineeringPromptCatalog());

        var result = behavior.Execute(ContextWith("SpecificationConformanceValidationRequest",
            new SpecificationConformanceValidationRequest(null, "log", "results"))) as SpecificationConformanceValidationResult;

        Assert.NotNull(result);
        Assert.True(result!.IsEscalated);
    }
}
```

- [ ] **Step 2: Run to confirm compile failure**

- [ ] **Step 3: Implement the shared types**

`src/Runtime/DomainContent/ImplementationValidation/ImplementationValidationTypes.cs`:
```csharp
using Runtime.DomainContent.EngineeringSpecificationReview;

namespace Runtime.DomainContent.ImplementationValidation;

public sealed record ConformanceFinding(string Area, string Classification, string Rationale);

public sealed record SpecificationConformanceValidationRequest(object? SpecificationInstance, object ImplementationLog, object TestPlanAndResults);

public sealed record SpecificationConformanceValidationResult
{
    public bool IsEscalated { get; }
    public string? EscalationReason { get; }
    public IReadOnlyList<ConformanceFinding>? ConformanceFindings { get; }
    public IReadOnlyList<string>? UncoveredSpecificationSections { get; }

    private SpecificationConformanceValidationResult(bool isEscalated, string? escalationReason, IReadOnlyList<ConformanceFinding>? conformanceFindings, IReadOnlyList<string>? uncoveredSpecificationSections)
    {
        IsEscalated = isEscalated;
        EscalationReason = escalationReason;
        ConformanceFindings = conformanceFindings;
        UncoveredSpecificationSections = uncoveredSpecificationSections;
    }

    public static SpecificationConformanceValidationResult Completed(IReadOnlyList<ConformanceFinding> findings, IReadOnlyList<string> uncoveredSections) =>
        new(false, null, findings, uncoveredSections);

    public static SpecificationConformanceValidationResult Escalated(string reason) => new(true, reason, null, null);
}

public sealed record ArchitectureStandardsConformanceReviewRequest(object TechnologyProfile, object Documentation, object? SpecificationInstance);

public sealed record ArchitectureStandardsConformanceReviewResult
{
    public bool IsEscalated { get; }
    public string? EscalationReason { get; }
    public IReadOnlyList<ConformanceFinding>? ConformanceFindings { get; }

    private ArchitectureStandardsConformanceReviewResult(bool isEscalated, string? escalationReason, IReadOnlyList<ConformanceFinding>? conformanceFindings)
    {
        IsEscalated = isEscalated;
        EscalationReason = escalationReason;
        ConformanceFindings = conformanceFindings;
    }

    public static ArchitectureStandardsConformanceReviewResult Completed(IReadOnlyList<ConformanceFinding> findings) => new(false, null, findings);

    public static ArchitectureStandardsConformanceReviewResult Escalated(string reason) => new(true, reason, null);
}

public sealed record ValidationFindingsSynthesisRequest(IReadOnlyList<ConformanceFinding> ConformanceFindings);

public sealed record ValidationFindingsSynthesisResult
{
    public bool IsEscalated { get; }
    public string? EscalationReason { get; }
    public EngineeringReviewInstance? ReviewInstance { get; }
    public string? MergeReadinessSignal { get; }

    private ValidationFindingsSynthesisResult(bool isEscalated, string? escalationReason, EngineeringReviewInstance? reviewInstance, string? mergeReadinessSignal)
    {
        IsEscalated = isEscalated;
        EscalationReason = escalationReason;
        ReviewInstance = reviewInstance;
        MergeReadinessSignal = mergeReadinessSignal;
    }

    public static ValidationFindingsSynthesisResult Completed(EngineeringReviewInstance reviewInstance, string mergeReadinessSignal) =>
        new(false, null, reviewInstance, mergeReadinessSignal);

    public static ValidationFindingsSynthesisResult Escalated(string reason) => new(true, reason, null, null);
}

internal sealed record AiConformanceFindingDto(string Area, string Classification, string Rationale);

internal sealed record AiConformanceValidationResponse(List<AiConformanceFindingDto> Conformance_Findings, List<string>? Uncovered_Specification_Sections);

internal sealed record AiArchitectureReviewResponse(List<AiConformanceFindingDto> Conformance_Findings);

internal sealed record AiSynthesisResponse(AiSynthesisReviewInstanceDto Review_Instance, string Merge_Readiness_Signal);

internal sealed record AiSynthesisReviewInstanceDto(string Review_Target, List<AiSynthesisFindingDto> Findings);

internal sealed record AiSynthesisFindingDto(string Area, string Severity, string Reason, string Evidence, string Recommendation);
```

- [ ] **Step 4: Implement `SpecificationConformanceValidationBehavior` (`SKL-EXE-0027`)**

`src/Runtime/DomainContent/ImplementationValidation/SpecificationConformanceValidationBehavior.cs`:
```csharp
using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.ImplementationValidation;

public sealed class SpecificationConformanceValidationBehavior : IBehavior
{
    private const string PromptContractId = "PCT-EXE-0027";
    private readonly AiExecutionService _aiExecutionService;
    private readonly IPromptCatalog _promptCatalog;

    public SpecificationConformanceValidationBehavior(AiExecutionService aiExecutionService, IPromptCatalog promptCatalog)
    {
        _aiExecutionService = aiExecutionService;
        _promptCatalog = promptCatalog;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("SpecificationConformanceValidationRequest", out var raw) || raw is not SpecificationConformanceValidationRequest request)
        {
            return SpecificationConformanceValidationResult.Escalated("No SpecificationConformanceValidationRequest was supplied in the execution context.");
        }

        var template = _promptCatalog.Get(PromptContractId)!;
        var renderResult = PromptRenderer.Render(template, new Dictionary<string, object?>
        {
            ["specification_instance"] = request.SpecificationInstance,
            ["implementation_log"] = request.ImplementationLog,
            ["test_plan_and_results"] = request.TestPlanAndResults,
        });

        if (!renderResult.Success)
        {
            return SpecificationConformanceValidationResult.Escalated($"Prompt rendering failed, missing: {string.Join(", ", renderResult.MissingVariables)}.");
        }

        var executionOutcome = _aiExecutionService.Execute(new AiPromptRequest(PromptContractId, template.SystemPrompt, renderResult.RenderedUserPrompt!), CancellationToken.None);
        if (!executionOutcome.Success)
        {
            return SpecificationConformanceValidationResult.Escalated(executionOutcome.FailureReason!);
        }

        if (!StructuredResponseParser.TryParse<AiConformanceValidationResponse>(executionOutcome.Result!.RawText, out var parsed, out var errors))
        {
            return SpecificationConformanceValidationResult.Escalated($"AI response for {PromptContractId} could not be parsed: {string.Join("; ", errors)}.");
        }

        var findings = parsed!.Conformance_Findings.Select(f => new ConformanceFinding(f.Area, f.Classification, f.Rationale)).ToList();
        return SpecificationConformanceValidationResult.Completed(findings, parsed.Uncovered_Specification_Sections ?? new List<string>());
    }
}
```

- [ ] **Step 5: Implement `ArchitectureStandardsConformanceReviewBehavior` (`SKL-EXE-0028`)**

`src/Runtime/DomainContent/ImplementationValidation/ArchitectureStandardsConformanceReviewBehavior.cs`:
```csharp
using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.ImplementationValidation;

public sealed class ArchitectureStandardsConformanceReviewBehavior : IBehavior
{
    private const string PromptContractId = "PCT-EXE-0028";
    private readonly AiExecutionService _aiExecutionService;
    private readonly IPromptCatalog _promptCatalog;

    public ArchitectureStandardsConformanceReviewBehavior(AiExecutionService aiExecutionService, IPromptCatalog promptCatalog)
    {
        _aiExecutionService = aiExecutionService;
        _promptCatalog = promptCatalog;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("ArchitectureStandardsConformanceReviewRequest", out var raw) || raw is not ArchitectureStandardsConformanceReviewRequest request)
        {
            return ArchitectureStandardsConformanceReviewResult.Escalated("No ArchitectureStandardsConformanceReviewRequest was supplied in the execution context.");
        }

        var template = _promptCatalog.Get(PromptContractId)!;
        var renderResult = PromptRenderer.Render(template, new Dictionary<string, object?>
        {
            ["technology_profile"] = request.TechnologyProfile,
            ["documentation"] = request.Documentation,
            ["specification_instance"] = request.SpecificationInstance,
        });

        if (!renderResult.Success)
        {
            return ArchitectureStandardsConformanceReviewResult.Escalated($"Prompt rendering failed, missing: {string.Join(", ", renderResult.MissingVariables)}.");
        }

        var executionOutcome = _aiExecutionService.Execute(new AiPromptRequest(PromptContractId, template.SystemPrompt, renderResult.RenderedUserPrompt!), CancellationToken.None);
        if (!executionOutcome.Success)
        {
            return ArchitectureStandardsConformanceReviewResult.Escalated(executionOutcome.FailureReason!);
        }

        if (!StructuredResponseParser.TryParse<AiArchitectureReviewResponse>(executionOutcome.Result!.RawText, out var parsed, out var errors))
        {
            return ArchitectureStandardsConformanceReviewResult.Escalated($"AI response for {PromptContractId} could not be parsed: {string.Join("; ", errors)}.");
        }

        var findings = parsed!.Conformance_Findings.Select(f => new ConformanceFinding(f.Area, f.Classification, f.Rationale)).ToList();
        return ArchitectureStandardsConformanceReviewResult.Completed(findings);
    }
}
```

- [ ] **Step 6: Implement `ValidationFindingsSynthesisBehavior` (`SKL-EXE-0029`)**

`src/Runtime/DomainContent/ImplementationValidation/ValidationFindingsSynthesisBehavior.cs`:
```csharp
using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Context;
using Runtime.DomainContent.EngineeringSpecificationReview;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.ImplementationValidation;

public sealed class ValidationFindingsSynthesisBehavior : IBehavior
{
    private const string PromptContractId = "PCT-EXE-0029";
    private readonly AiExecutionService _aiExecutionService;
    private readonly IPromptCatalog _promptCatalog;

    public ValidationFindingsSynthesisBehavior(AiExecutionService aiExecutionService, IPromptCatalog promptCatalog)
    {
        _aiExecutionService = aiExecutionService;
        _promptCatalog = promptCatalog;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("ValidationFindingsSynthesisRequest", out var raw) || raw is not ValidationFindingsSynthesisRequest request)
        {
            return ValidationFindingsSynthesisResult.Escalated("No ValidationFindingsSynthesisRequest was supplied in the execution context.");
        }

        var template = _promptCatalog.Get(PromptContractId)!;
        var renderResult = PromptRenderer.Render(template, new Dictionary<string, object?>
        {
            ["conformance_findings"] = request.ConformanceFindings,
        });

        if (!renderResult.Success)
        {
            return ValidationFindingsSynthesisResult.Escalated($"Prompt rendering failed, missing: {string.Join(", ", renderResult.MissingVariables)}.");
        }

        var executionOutcome = _aiExecutionService.Execute(new AiPromptRequest(PromptContractId, template.SystemPrompt, renderResult.RenderedUserPrompt!), CancellationToken.None);
        if (!executionOutcome.Success)
        {
            return ValidationFindingsSynthesisResult.Escalated(executionOutcome.FailureReason!);
        }

        if (!StructuredResponseParser.TryParse<AiSynthesisResponse>(executionOutcome.Result!.RawText, out var parsed, out var errors))
        {
            return ValidationFindingsSynthesisResult.Escalated($"AI response for {PromptContractId} could not be parsed: {string.Join("; ", errors)}.");
        }

        var reviewInstance = new EngineeringReviewInstance(
            parsed!.Review_Instance.Review_Target,
            ReviewIteration: 1,
            parsed.Review_Instance.Findings.Select(f => new EngineeringReviewFinding(f.Area, f.Severity, f.Reason, f.Evidence, f.Recommendation)).ToList());

        return ValidationFindingsSynthesisResult.Completed(reviewInstance, parsed.Merge_Readiness_Signal);
    }
}
```

- [ ] **Step 7: Run to confirm green**

Run: `dotnet test tests/Runtime.Tests/Runtime.Tests.csproj --filter ImplementationValidationBehaviorTests`
Expected: 4/4 pass (add `[JsonPropertyName]` attributes as in Task 7 Step 6 if snake_case binding doesn't resolve automatically).

- [ ] **Step 8: Commit**

```bash
git add src/Runtime/DomainContent/ImplementationValidation tests/Runtime.Tests/DomainContent/ImplementationValidation
git commit -m "feat(ai): implement AGT-EXE-0014 Validation Agent via SKL-EXE-0027/28/29"
```

---

## Task 9: Wire the four new behaviors into `EngineeringExecutionPipeline`

**Files:**
- Modify: `src/Runtime/Execution/EngineeringExecutionPipeline.cs`
- Modify: `src/Runtime/DomainContent/ConversationManagement/ConversationIntakeAndRoutingBehavior.cs`
- Test: `tests/Runtime.Tests/Execution/EngineeringExecutionPipelineAiTests.cs`

**Interfaces:**
- Consumes: `IBehavior` implementations from Task 7/8, `EngineeringExecutionPipeline.InvokeSkill<T>` (existing, unmodified), `ExecutionResult` (existing, unmodified).
- Produces: two new capability dispatch entries — `"WFL-DEL-0007"` (Specification Review, `AGT-EXE-0013`) and `"WFL-DEL-0005"` (Implementation Validation, `AGT-EXE-0014`) — and two new routing keywords (`"review specification"`, `"merge readiness"`).

- [ ] **Step 1: Add the two routing keywords (append, don't reorder existing entries)**

`src/Runtime/DomainContent/ConversationManagement/ConversationIntakeAndRoutingBehavior.cs` — append to `KnownCapabilities` (after the existing `("validate", "REPO-VALIDATION")` entry, before the closing `};`):
```csharp
        ("review specification", "WFL-DEL-0007"),
        ("merge readiness", "WFL-DEL-0005"),
```

Also update the class doc comment (line 13-16) to mention `WFL-DEL-0007` and `WFL-DEL-0005`, and the clarification message (`"Supported: feature, epic, delivery, analyze, plan, validate."`) to append `, review specification, merge readiness`.

- [ ] **Step 2: Write the failing pipeline-integration tests**

`tests/Runtime.Tests/Execution/EngineeringExecutionPipelineAiTests.cs` — follow the exact construction pattern every other `EngineeringExecutionPipelineTests.cs` test already uses to boot a `RuntimeHandle` (read `tests/Runtime.Tests/Execution/EngineeringExecutionPipelineTests.cs` first for the exact `RuntimeBootstrapper.Boot(...)` call and behavior-dictionary shape used there), but add `["SKL-EXE-0026"]`, `["SKL-EXE-0027"]`, `["SKL-EXE-0028"]`, `["SKL-EXE-0029"]` to the behaviors dictionary, each backed by a stub `IAiProvider` returning canned JSON (same `StubProvider` pattern as Tasks 7/8), and register the four new artifact IDs as `Active` in whatever `IRegistry` seeding that existing test file uses. Two test cases:
```csharp
[Fact]
public void RoutesReviewSpecificationRequestToWfl0007AndReturnsFinalRecommendation()
{
    // Arrange: boot RuntimeHandle with SKL-EXE-0021/0022/0026 behaviors bound,
    // SKL-EXE-0026 backed by a StubProvider returning a review_complete JSON body.
    // Act: pipeline.Execute(new EngineeringRequest("please review specification for SPEC-1",
    //     "default-profile", new Dictionary<string, object> { ["SpecificationReviewRequest"] = ... }));
    // Assert: result.Success is true, result.ExecutedWorkflowId == "WFL-DEL-0007".
}

[Fact]
public void RoutesMergeReadinessRequestToWfl0005AndChainsAllThreeValidationSkills()
{
    // Arrange: boot RuntimeHandle with SKL-EXE-0021/0022/0027/0028/0029 bound.
    // Act: pipeline.Execute(new EngineeringRequest("check merge readiness for this implementation",
    //     "default-profile", payload));
    // Assert: result.ExecutedWorkflowId == "WFL-DEL-0005", result.Success reflects merge_readiness_signal == "ready".
}
```
(Fill in the arrange/payload details by mirroring `FromEngineeringPlanning`'s own existing test in `EngineeringExecutionPipelineTests.cs` line ~224 exactly — same `EngineeringRequest`/`Payload` shape, same `RuntimeBootstrapper.Boot` call — do not invent a different construction path.)

- [ ] **Step 3: Run to confirm it fails (no dispatch target yet)**

Run: `dotnet test tests/Runtime.Tests/Runtime.Tests.csproj --filter EngineeringExecutionPipelineAiTests`
Expected: fails with `"No executable dispatch target for capability 'WFL-DEL-0007'"` / `'WFL-DEL-0005'`.

- [ ] **Step 4: Add the two dispatch cases**

`src/Runtime/Execution/EngineeringExecutionPipeline.cs` — in `Dispatch`'s `switch`, add two new arms right after the `"REPO-VALIDATION"` line:
```csharp
            "WFL-DEL-0007" => FromEngineeringSpecificationReview(request),
            "WFL-DEL-0005" => FromImplementationValidation(request),
```

- [ ] **Step 5: Implement `FromEngineeringSpecificationReview`**

Add to `EngineeringExecutionPipeline`, near `FromRepositoryValidation`:
```csharp
    private ExecutionResult FromEngineeringSpecificationReview(EngineeringRequest request)
    {
        if (request.Payload is null || !request.Payload.TryGetValue("SpecificationReviewRequest", out var raw)
            || raw is not DomainContent.EngineeringSpecificationReview.SpecificationReviewRequest reviewRequest)
        {
            return new ExecutionResult(false, "WFL-DEL-0007", "AGT-EXE-0013",
                new[] { "Payload must carry a SpecificationReviewRequest." }, null, TimeSpan.Zero, null, null);
        }

        var outcome = InvokeSkill<DomainContent.EngineeringSpecificationReview.SpecificationReviewOutcome>(
            "SKL-EXE-0026", "SpecificationReviewRequest", reviewRequest, request.ProjectProfileId, "SpecificationReview");

        if (outcome is null)
        {
            return new ExecutionResult(false, "WFL-DEL-0007", "AGT-EXE-0013", new[] { "SKL-EXE-0026 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        if (outcome.IsEscalated)
        {
            return new ExecutionResult(false, "WFL-DEL-0007", "AGT-EXE-0013", new[] { outcome.EscalationReason! }, null, TimeSpan.Zero, null, null);
        }

        var findingsSummary = outcome.ReviewInstance!.Findings.Select(f => $"[{f.Severity}] {f.Area}: {f.Reason}").ToList();
        return new ExecutionResult(true, "WFL-DEL-0007", "AGT-EXE-0013", findingsSummary, null, TimeSpan.Zero,
            outcome.FinalRecommendation, "Independent review complete.");
    }

    private ExecutionResult FromImplementationValidation(EngineeringRequest request)
    {
        if (request.Payload is null
            || !request.Payload.TryGetValue("SpecificationConformanceValidationRequest", out var confRaw)
            || confRaw is not DomainContent.ImplementationValidation.SpecificationConformanceValidationRequest confRequest
            || !request.Payload.TryGetValue("ArchitectureStandardsConformanceReviewRequest", out var archRaw)
            || archRaw is not DomainContent.ImplementationValidation.ArchitectureStandardsConformanceReviewRequest archRequest)
        {
            return new ExecutionResult(false, "WFL-DEL-0005", "AGT-EXE-0014",
                new[] { "Payload must carry SpecificationConformanceValidationRequest and ArchitectureStandardsConformanceReviewRequest." }, null, TimeSpan.Zero, null, null);
        }

        var conformance = InvokeSkill<DomainContent.ImplementationValidation.SpecificationConformanceValidationResult>(
            "SKL-EXE-0027", "SpecificationConformanceValidationRequest", confRequest, request.ProjectProfileId, "Validation-Conformance");
        if (conformance is null || conformance.IsEscalated)
        {
            return new ExecutionResult(false, "WFL-DEL-0005", "AGT-EXE-0014",
                new[] { conformance?.EscalationReason ?? "SKL-EXE-0027 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var architecture = InvokeSkill<DomainContent.ImplementationValidation.ArchitectureStandardsConformanceReviewResult>(
            "SKL-EXE-0028", "ArchitectureStandardsConformanceReviewRequest", archRequest, request.ProjectProfileId, "Validation-Architecture");
        if (architecture is null || architecture.IsEscalated)
        {
            return new ExecutionResult(false, "WFL-DEL-0005", "AGT-EXE-0014",
                new[] { architecture?.EscalationReason ?? "SKL-EXE-0028 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var combinedFindings = conformance.ConformanceFindings!.Concat(architecture.ConformanceFindings!).ToList();
        var synthesis = InvokeSkill<DomainContent.ImplementationValidation.ValidationFindingsSynthesisResult>(
            "SKL-EXE-0029", "ValidationFindingsSynthesisRequest",
            new DomainContent.ImplementationValidation.ValidationFindingsSynthesisRequest(combinedFindings),
            request.ProjectProfileId, "Validation-Synthesis");

        if (synthesis is null || synthesis.IsEscalated)
        {
            return new ExecutionResult(false, "WFL-DEL-0005", "AGT-EXE-0014",
                new[] { synthesis?.EscalationReason ?? "SKL-EXE-0029 is not invocable." }, null, TimeSpan.Zero, null, null);
        }

        var diagnosticsSummary = conformance.UncoveredSpecificationSections!
            .Select(s => $"Uncovered specification section: {s}")
            .Concat(synthesis.ReviewInstance!.Findings.Select(f => $"[{f.Severity}] {f.Area}: {f.Reason}"))
            .ToList();

        var isReady = synthesis.MergeReadinessSignal == "ready";
        return new ExecutionResult(isReady, "WFL-DEL-0005", "AGT-EXE-0014", diagnosticsSummary, null, TimeSpan.Zero,
            synthesis.MergeReadinessSignal,
            isReady ? "No further action needed." : "Awaiting Human Approval at the Review gate.");
    }
```

- [ ] **Step 6: Run to confirm green**

Run: `dotnet test tests/Runtime.Tests/Runtime.Tests.csproj --filter EngineeringExecutionPipelineAiTests`

- [ ] **Step 7: Run the full solution suite**

Run: `dotnet test AefPlatform.sln`
Expected: every pre-existing test still green, plus all new tests.

- [ ] **Step 8: Commit**

```bash
git add src/Runtime/Execution/EngineeringExecutionPipeline.cs src/Runtime/DomainContent/ConversationManagement/ConversationIntakeAndRoutingBehavior.cs tests/Runtime.Tests/Execution/EngineeringExecutionPipelineAiTests.cs
git commit -m "feat(ai): dispatch WFL-DEL-0007 and WFL-DEL-0005 to the new AI-backed Reviewer/Validation behaviors"
```

---

## Task 10: Host wiring — configuration, provider selection, DI, manifest

**Files:**
- Create: `src/Host/AiProviderOptions.cs`
- Modify: `src/Host/Program.cs`
- Modify: `src/Host/appsettings.json`
- Modify: `src/Host/EngineeringArchitectureManifest.cs`
- Test: `tests/Host.Tests/GA1AiIntegrationHostTests.cs`

**Interfaces:**
- Consumes: `OpenAiProvider`/`AzureOpenAiProvider` (Tasks 5/6), `AiExecutionService` (Task 2), `EngineeringPromptCatalog` (Task 4), the four `IBehavior`s (Tasks 7/8).
- Produces: `AiProviderOptions { string Provider; OpenAiProviderOptions? OpenAi; AzureOpenAiProviderOptions? AzureOpenAi; int TimeoutSeconds = 60; }` bound from configuration section `"Ai"`.

- [ ] **Step 1: Define `AiProviderOptions`**

`src/Host/AiProviderOptions.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using Runtime.Ai.Providers;

namespace AefPlatform.Host;

// Bound the same way PlatformHostOptions already is
// (.Bind().ValidateDataAnnotations().ValidateOnStart()) - Provider
// selects which of OpenAi/AzureOpenAi is actually constructed; the
// other section may be absent. Real API keys are never checked into
// appsettings.json - see Step 4's placeholder values.
public sealed class AiProviderOptions
{
    [Required]
    [RegularExpression("OpenAI|AzureOpenAI")]
    public string Provider { get; set; } = "OpenAI";

    public int TimeoutSeconds { get; set; } = 60;

    public OpenAiSection? OpenAi { get; set; }

    public AzureOpenAiSection? AzureOpenAi { get; set; }

    public sealed class OpenAiSection
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-4o";
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    }

    public sealed class AzureOpenAiSection
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Deployment { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = "2024-06-01";
    }
}
```

- [ ] **Step 2: Add the `"Ai"` section to `appsettings.json` (no real secrets)**

`src/Host/appsettings.json` — add alongside the existing `"AefPlatform"`/`"Logging"` keys:
```json
{
  "AefPlatform": { "PlatformName": "AEF Platform Runtime", "FailFastOnRegistrationError": true },
  "Ai": {
    "Provider": "OpenAI",
    "TimeoutSeconds": 60,
    "OpenAi": { "ApiKey": "", "Model": "gpt-4o", "BaseUrl": "https://api.openai.com/v1" },
    "AzureOpenAi": { "Endpoint": "", "ApiKey": "", "Deployment": "", "ApiVersion": "2024-06-01" }
  },
  "Logging": { "LogLevel": { "Default": "Information" } }
}
```
Real `ApiKey`/`Endpoint` values are supplied at deploy time via environment variable overrides (`Ai__OpenAi__ApiKey`, `Ai__AzureOpenAi__ApiKey`, etc. — .NET configuration's standard `__`-separated env var binding, already implicitly available since `Host.CreateApplicationBuilder` wires the environment-variable configuration provider by default) — never committed to this file.

- [ ] **Step 3: Wire DI in `Program.cs`**

`src/Host/Program.cs` — add after the existing `AddOptions<PlatformHostOptions>()` block:
```csharp
builder.Services
    .AddOptions<AiProviderOptions>()
    .Bind(builder.Configuration.GetSection("Ai"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IAiProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AiProviderOptions>>().Value;
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return options.Provider switch
    {
        "AzureOpenAI" => new AzureOpenAiProvider(httpClientFactory.CreateClient(), new AzureOpenAiProviderOptions
        {
            Endpoint = options.AzureOpenAi!.Endpoint,
            ApiKey = options.AzureOpenAi.ApiKey,
            Deployment = options.AzureOpenAi.Deployment,
            ApiVersion = options.AzureOpenAi.ApiVersion,
        }),
        _ => new OpenAiProvider(httpClientFactory.CreateClient(), new OpenAiProviderOptions
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
```
Add the required `using` directives at the top: `using Microsoft.Extensions.Options;`, `using Runtime.Ai;`, `using Runtime.Ai.Prompts;`, `using Runtime.Ai.Providers;`, `using Runtime.DomainContent.EngineeringSpecificationReview;`, `using Runtime.DomainContent.ImplementationValidation;`.

Then change the existing `builder.Services.AddSingleton(_ => { ... RuntimeBootstrapper.Boot ... })` factory to `builder.Services.AddSingleton(sp => { ... })` (capture `sp` instead of discarding it) and add the four new entries to the `behaviors` dictionary:
```csharp
        ["SKL-EXE-0026"] = new SpecificationIndependentReviewBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
        ["SKL-EXE-0027"] = new SpecificationConformanceValidationBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
        ["SKL-EXE-0028"] = new ArchitectureStandardsConformanceReviewBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
        ["SKL-EXE-0029"] = new ValidationFindingsSynthesisBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
```
Since `AiExecutionService`/`IPromptCatalog` must be registered **before** this factory runs, keep them declared earlier in `Program.cs` (Step 3's blocks go before this existing `AddSingleton` call, not after).

- [ ] **Step 4: Add the four artifacts to the manifest**

`src/Host/EngineeringArchitectureManifest.cs` — append to `KnownArtifacts`:
```csharp
        new ArchitectureArtifact("SKL-EXE-0026", "Execution"),
        new ArchitectureArtifact("WFL-DEL-0007", "Delivery"),
        new ArchitectureArtifact("SKL-EXE-0027", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0028", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0029", "Execution"),
```
(`AGT-EXE-0013`/`AGT-EXE-0014`/`WFL-DEL-0005` are Agent/Workflow ids, not directly in this Skill/Workflow-artifact manifest style used elsewhere — check whether `AGT-EXE-0012`/`WFL-DEL-0006` appear in the existing list as a precedent before deciding whether to also add `AGT-EXE-0013`/`AGT-EXE-0014`/`WFL-DEL-0005`; the existing list does include `AGT-EXE-0012` and `WFL-DEL-0006`, so add `AGT-EXE-0013`, `AGT-EXE-0014` as `"Execution"` entries and `WFL-DEL-0005` as a `"Delivery"` entry too, matching that precedent exactly.)

- [ ] **Step 5: Write the failing Host-level test**

`tests/Host.Tests/GA1AiIntegrationHostTests.cs` — mirror whatever existing `Host.Tests` file boots the full `Host` (`WebApplicationFactory`-style or direct `Host.CreateApplicationBuilder` in tests — read one existing `Host.Tests` file first to copy its exact boot pattern) and assert:
```csharp
// 1. The Host builds and starts successfully with the appsettings.json
//    "Ai" section's default (empty-string) OpenAI key present (construction
//    must not throw even with an empty key - only a real HTTP call would fail).
// 2. RuntimeHandle's IRegistry reports SKL-EXE-0026/27/28/29 and
//    WFL-DEL-0007 as Active (EngineeringArchitectureManifest wiring worked).
```

- [ ] **Step 6: Run to confirm it fails, then fix Program.cs/manifest until green**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj`

- [ ] **Step 7: Run the full solution suite one more time**

Run: `dotnet test AefPlatform.sln`
Expected: 100% green, including the pre-existing 324.

- [ ] **Step 8: Commit**

```bash
git add src/Host tests/Host.Tests
git commit -m "feat(ai): wire AI provider selection, DI, and manifest registration into the Host"
```

---

## Task 11: Final verification, PR, merge, cleanup

- [ ] **Step 1: Full clean build + test run**

Run: `dotnet build AefPlatform.sln` then `dotnet test AefPlatform.sln`
Expected: 0 warnings, 0 failures, full count = 324 (pre-existing) + all new GA-1 tests.

- [ ] **Step 2: Confirm working tree is clean**

Run: `git status`
Expected: nothing to commit.

- [ ] **Step 3: Push and open PR**

```bash
git push -u origin feature/ga1-ai-integration-layer
gh pr create --title "GA-1: AI Integration Layer & Engineering Intelligence Runtime" --body "$(cat <<'EOF'
## Summary
- Provider-neutral AI Integration Layer (Runtime.Ai): IAiProvider abstraction, AiExecutionService (retry/timeout/cancellation/diagnostics, built on the existing RetryExecutor + IEvidenceSink), Prompt Execution Engine (template/render/structured-parse).
- OpenAI and Azure OpenAI providers, both plain HttpClient/System.Text.Json, no new NuGet dependency.
- Real implementations of AGT-EXE-0013 (Specification Reviewer, SKL-EXE-0026) and AGT-EXE-0014 (Validation Agent, SKL-EXE-0027/28/29), the two capabilities RC0's own "Recommendation for GA Readiness" named as GA-blocking.
- Wired into EngineeringExecutionPipeline via two new dispatch cases (WFL-DEL-0007, WFL-DEL-0005), following the existing InvokeSkill<T> chaining pattern unmodified. Deterministic dispatch (AGT-EXE-0012 and earlier) is untouched.

## Test plan
- [ ] `dotnet test AefPlatform.sln` green, 324 pre-existing tests unaffected
- [ ] New Runtime.Ai.Tests, Runtime.Tests, Host.Tests all green
EOF
)"
```

- [ ] **Step 4: Merge once CI/self-review is clean**

Per this repo's own standing convention (no separate per-PR confirmation once review is clean): `gh pr merge --auto --squash` (or plain `gh pr merge --squash` if auto-merge isn't enabled on this repo).

- [ ] **Step 5: Delete the merged branch**

```bash
git checkout main
git pull
git branch -d feature/ga1-ai-integration-layer
git push origin --delete feature/ga1-ai-integration-layer
```
