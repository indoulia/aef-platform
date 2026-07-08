# GA-3 Capability 1: AEF Runtime Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement GA-3 Capability 1 (AEF Runtime Host) per `docs/superpowers/specs/2026-07-08-ga3-capability1-runtime-host-design.md`: fix two real concurrency bugs found in the existing DI graph, and add a security seam (`ISecurityEvaluator` + trusted-default implementation) that future adapters will call before submitting an `ExecutionRequest`.

**Architecture:** Two independent, localized bug fixes (`InMemoryEvidenceSink` thread-safety, `InMemorySessionStore.Touch` atomicity) plus one new additive capability: `ISecurityEvaluator`/`SecurityContext`/`SecurityDecision` as new files in `Runtime.Agent.Security` (reachable by future adapters, which per ADR-0025 depend on `Runtime.Agent` only), with `TrustedDefaultSecurityEvaluator` and its DI registration in `Host` (the composition root).

**Tech Stack:** .NET 8, `Microsoft.Extensions.Hosting`/`Options` (already referenced), xUnit (existing test convention across all test projects).

## Global Constraints

- Target framework: `net8.0` (all projects, unchanged).
- No new NuGet package dependency — BCL + `Microsoft.Extensions.Hosting`/`Options` only, matching the repo's existing convention.
- `Runtime.Agent.csproj` continues to reference `Runtime` only — no new project reference is added by this plan.
- No existing GA-1/GA-2 contract changes: `ExecutionRequest`, `ExecutionResponse`, `IAgentExecutionInterface`, `ISessionStore`'s interface shape, `ICapabilityCatalog`, `IExecutionProgressObserver` are all untouched. `InMemorySessionStore.Touch`'s *implementation* changes (not its interface signature) as a correctness fix only.
- `EngineeringExecutionPipeline` is not modified.
- The full existing test suite (324+ tests through RC0/GA-1/GA-2) must stay green throughout — re-run as the final gate (Task 5).
- No REST or CLI adapter code — out of scope for this Capability (Capabilities 2 and 3).
- No health/diagnostics abstraction of any kind — out of scope for this Capability (Capability 4), per the Independent Engineering Review's resolved finding.

---

### Task 1: Fix `InMemoryEvidenceSink` thread-safety

**Files:**
- Modify: `src/Kernel/Evidence/InMemoryEvidenceSink.cs`
- Test: `tests/Kernel.Tests/Evidence/InMemoryEvidenceSinkTests.cs` (create)

**Interfaces:**
- Consumes: `Kernel.Evidence.IEvidenceSink` (unchanged), `Kernel.Evidence.EvidenceRecord(string Contract, string Decision, string SubjectId, string? Reason, DateTimeOffset Timestamp)` (unchanged).
- Produces: `InMemoryEvidenceSink.Record(EvidenceRecord)` and `InMemoryEvidenceSink.Records` (`IReadOnlyList<EvidenceRecord>`) — same public signatures as today, now thread-safe.

- [ ] **Step 1: Write the failing test**

Create `tests/Kernel.Tests/Evidence/InMemoryEvidenceSinkTests.cs`:

```csharp
using Kernel.Evidence;

namespace Kernel.Tests.Evidence;

public class InMemoryEvidenceSinkTests
{
    [Fact]
    public void Record_SingleCall_AppearsInRecords()
    {
        var sink = new InMemoryEvidenceSink();
        var record = new EvidenceRecord("Contract", "Completed", "subject-1", null, DateTimeOffset.UtcNow);

        sink.Record(record);

        Assert.Single(sink.Records);
        Assert.Equal(record, sink.Records[0]);
    }

    [Fact]
    public void Record_ConcurrentCalls_AllRecordsAreCapturedWithoutCorruption()
    {
        var sink = new InMemoryEvidenceSink();
        const int threadCount = 20;
        const int recordsPerThread = 200;

        Parallel.For(0, threadCount, i =>
        {
            for (var j = 0; j < recordsPerThread; j++)
            {
                sink.Record(new EvidenceRecord("Contract", "Completed", $"subject-{i}-{j}", null, DateTimeOffset.UtcNow));
            }
        });

        Assert.Equal(threadCount * recordsPerThread, sink.Records.Count);
    }
}
```

- [ ] **Step 2: Run the test to verify the concurrency test fails against the current (unfixed) code**

Run: `dotnet test tests/Kernel.Tests/Kernel.Tests.csproj --filter "FullyQualifiedName~InMemoryEvidenceSinkTests"`
Expected: `Record_SingleCall_AppearsInRecords` passes; `Record_ConcurrentCalls_AllRecordsAreCapturedWithoutCorruption` fails (an exception from the unsynchronized `List<T>`, or `Records.Count` not equal to 4000) — this is a race condition, so if it happens not to fail on the first run, re-run 2-3 times; `List<T>.Add` from 20 threads × 200 iterations reliably corrupts on virtually all real hardware.

- [ ] **Step 3: Implement the fix**

Replace the full contents of `src/Kernel/Evidence/InMemoryEvidenceSink.cs`:

```csharp
namespace Kernel.Evidence;

// Default sink absent a registered Service. Non-authoritative: its output
// is not governed Evidence (no Lifecycle status, not Review-consumable)
// until the real Knowledge & Evidence Store is wired in at M2 — per this
// Epic's own ADR, Option 3's stated Consequences.
public sealed class InMemoryEvidenceSink : IEvidenceSink
{
    private readonly object _lock = new();
    private readonly List<EvidenceRecord> _records = new();

    // AiExecutionService is registered as a process-wide singleton with
    // this as its default evidence sink when none is supplied (Program.cs),
    // so concurrent adapter requests can call Record from multiple threads
    // at once - List<T> is not thread-safe for concurrent writes without
    // this lock (GA-3 Capability 1 concurrency audit).
    public IReadOnlyList<EvidenceRecord> Records
    {
        get
        {
            lock (_lock)
            {
                return _records.ToArray();
            }
        }
    }

    public void Record(EvidenceRecord record)
    {
        lock (_lock)
        {
            _records.Add(record);
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Kernel.Tests/Kernel.Tests.csproj --filter "FullyQualifiedName~InMemoryEvidenceSinkTests"`
Expected: both tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Kernel/Evidence/InMemoryEvidenceSink.cs tests/Kernel.Tests/Evidence/InMemoryEvidenceSinkTests.cs
git commit -m "fix(kernel): guard InMemoryEvidenceSink against concurrent writes"
```

---

### Task 2: Fix `InMemorySessionStore.Touch` atomicity

**Files:**
- Modify: `src/Runtime.Agent/Sessions/InMemorySessionStore.cs`
- Modify: `tests/Runtime.Agent.Tests/Sessions/InMemorySessionStoreTests.cs`

**Interfaces:**
- Consumes: `Runtime.Agent.Sessions.ISessionStore` (unchanged), `Runtime.Agent.Sessions.ExecutionSession` (unchanged).
- Produces: `InMemorySessionStore.Touch(string sessionId, string? projectProfileId, IReadOnlyDictionary<string,object>? payload) : ExecutionSession` — same signature and same unknown-id `KeyNotFoundException` behavior as today, now atomic.

- [ ] **Step 1: Write the failing test**

Append this test method inside the existing `InMemorySessionStoreTests` class in `tests/Runtime.Agent.Tests/Sessions/InMemorySessionStoreTests.cs` (add it after `Touch_UnknownSessionId_Throws`, before the closing `}` of the class):

```csharp
    [Fact]
    public void Touch_ConcurrentCallsOnSameSession_NoLostUpdates()
    {
        var store = new InMemorySessionStore();
        var created = store.Create("default-profile");
        const int callCount = 200;

        Parallel.For(0, callCount, _ => store.Touch(created.SessionId, "default-profile", null));

        var final = store.Get(created.SessionId);
        Assert.Equal(callCount, final!.TurnCount);
    }
```

- [ ] **Step 2: Run the test to verify it fails against the current (unfixed) code**

Run: `dotnet test tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj --filter "FullyQualifiedName~InMemorySessionStoreTests"`
Expected: `Touch_ConcurrentCallsOnSameSession_NoLostUpdates` fails — `final.TurnCount` is less than 200 (lost updates from the non-atomic read-then-write). If it happens to pass on a given run (races aren't 100% guaranteed to manifest every time), re-run 2-3 times; 200 concurrent calls on one `ConcurrentDictionary` entry reliably exposes the race on real hardware.

- [ ] **Step 3: Implement the fix**

In `src/Runtime.Agent/Sessions/InMemorySessionStore.cs`, replace the `Touch` method body:

```csharp
    public ExecutionSession Touch(string sessionId, string? projectProfileId, IReadOnlyDictionary<string, object>? payload)
    {
        // AddOrUpdate's updateValueFactory runs atomically against the
        // dictionary's current value (retried internally if another
        // thread wins a race), closing the read-then-write gap the
        // previous TryGetValue-then-index-write pair left open (GA-3
        // Capability 1 concurrency audit): two overlapping Touch calls on
        // the same session id could otherwise lose one call's TurnCount
        // increment. addValueFactory only runs for an unknown id,
        // preserving the original throw-on-unknown-session behavior.
        return _sessions.AddOrUpdate(
            sessionId,
            addValueFactory: _ => throw new KeyNotFoundException($"No session exists for id '{sessionId}'."),
            updateValueFactory: (_, existing) => existing with
            {
                LastActivityAt = DateTimeOffset.UtcNow,
                LastProjectProfileId = projectProfileId ?? existing.LastProjectProfileId,
                LastPayload = payload ?? existing.LastPayload,
                TurnCount = existing.TurnCount + 1,
            });
    }
```

The full file (for reference — only the `Touch` method body changes; `Create` and `Get` are untouched):

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
        // AddOrUpdate's updateValueFactory runs atomically against the
        // dictionary's current value (retried internally if another
        // thread wins a race), closing the read-then-write gap the
        // previous TryGetValue-then-index-write pair left open (GA-3
        // Capability 1 concurrency audit): two overlapping Touch calls on
        // the same session id could otherwise lose one call's TurnCount
        // increment. addValueFactory only runs for an unknown id,
        // preserving the original throw-on-unknown-session behavior.
        return _sessions.AddOrUpdate(
            sessionId,
            addValueFactory: _ => throw new KeyNotFoundException($"No session exists for id '{sessionId}'."),
            updateValueFactory: (_, existing) => existing with
            {
                LastActivityAt = DateTimeOffset.UtcNow,
                LastProjectProfileId = projectProfileId ?? existing.LastProjectProfileId,
                LastPayload = payload ?? existing.LastPayload,
                TurnCount = existing.TurnCount + 1,
            });
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Runtime.Agent.Tests/Runtime.Agent.Tests.csproj --filter "FullyQualifiedName~InMemorySessionStoreTests"`
Expected: all `InMemorySessionStoreTests` tests PASS, including the new one and the four pre-existing ones (`Create_ProducesEmptySessionState`, `Get_UnknownSessionId_ReturnsNull`, `Touch_UpdatesLastActivityTurnCountAndMergesPayload`, `Touch_OmittedPayload_RetainsPriorPayload`, `Touch_UnknownSessionId_Throws`).

- [ ] **Step 5: Commit**

```bash
git add src/Runtime.Agent/Sessions/InMemorySessionStore.cs tests/Runtime.Agent.Tests/Sessions/InMemorySessionStoreTests.cs
git commit -m "fix(runtime.agent): make InMemorySessionStore.Touch atomic under concurrent calls"
```

---

### Task 3: Add the security seam contracts and trusted-default implementation

**Files:**
- Create: `src/Runtime.Agent/Security/SecurityContext.cs`
- Create: `src/Runtime.Agent/Security/SecurityDecision.cs`
- Create: `src/Runtime.Agent/Security/ISecurityEvaluator.cs`
- Create: `src/Host/TrustedDefaultSecurityEvaluator.cs`
- Test: `tests/Host.Tests/TrustedDefaultSecurityEvaluatorTests.cs` (create)

**Interfaces:**
- Consumes: nothing new — these are new, additive types with no dependency on existing GA-2 contracts.
- Produces:
  - `Runtime.Agent.Security.SecurityContext(string? PrincipalId, IReadOnlyDictionary<string,string>? Claims)`
  - `Runtime.Agent.Security.SecurityDecision(bool IsAllowed, string? DenialReason)`
  - `Runtime.Agent.Security.ISecurityEvaluator { SecurityDecision Evaluate(SecurityContext context); }`
  - `AefPlatform.Host.TrustedDefaultSecurityEvaluator : ISecurityEvaluator` — used by Task 4's DI registration.

- [ ] **Step 1: Create the security contracts in `Runtime.Agent`**

Create `src/Runtime.Agent/Security/SecurityContext.cs`:

```csharp
namespace Runtime.Agent.Security;

public sealed record SecurityContext(string? PrincipalId, IReadOnlyDictionary<string, string>? Claims);
```

Create `src/Runtime.Agent/Security/SecurityDecision.cs`:

```csharp
namespace Runtime.Agent.Security;

public sealed record SecurityDecision(bool IsAllowed, string? DenialReason);
```

Create `src/Runtime.Agent/Security/ISecurityEvaluator.cs`:

```csharp
namespace Runtime.Agent.Security;

public interface ISecurityEvaluator
{
    SecurityDecision Evaluate(SecurityContext context);
}
```

- [ ] **Step 2: Write the failing test for the trusted-default implementation**

Create `tests/Host.Tests/TrustedDefaultSecurityEvaluatorTests.cs`:

```csharp
using AefPlatform.Host;
using Microsoft.Extensions.Logging.Abstractions;
using Runtime.Agent.Security;

namespace Host.Tests;

public class TrustedDefaultSecurityEvaluatorTests
{
    private static TrustedDefaultSecurityEvaluator CreateEvaluator() =>
        new(NullLogger<TrustedDefaultSecurityEvaluator>.Instance);

    [Fact]
    public void Evaluate_EmptyContext_Allows()
    {
        var evaluator = CreateEvaluator();

        var decision = evaluator.Evaluate(new SecurityContext(null, null));

        Assert.True(decision.IsAllowed);
        Assert.Null(decision.DenialReason);
    }

    [Fact]
    public void Evaluate_ContextWithClaims_Allows()
    {
        var evaluator = CreateEvaluator();
        var context = new SecurityContext("user-1", new Dictionary<string, string> { ["role"] = "engineer" });

        var decision = evaluator.Evaluate(context);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void Evaluate_NullClaims_StillAllows()
    {
        var evaluator = CreateEvaluator();
        var context = new SecurityContext("user-1", null);

        var decision = evaluator.Evaluate(context);

        Assert.True(decision.IsAllowed);
    }
}
```

`NullLogger<T>` comes from `Microsoft.Extensions.Logging.Abstractions`, already a transitive dependency of `Microsoft.Extensions.Hosting` (no new package reference).

- [ ] **Step 3: Run the test to verify it fails to compile (the type doesn't exist yet)**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj --filter "FullyQualifiedName~TrustedDefaultSecurityEvaluatorTests"`
Expected: build FAILS — `TrustedDefaultSecurityEvaluator` does not exist.

- [ ] **Step 4: Implement `TrustedDefaultSecurityEvaluator`**

Create `src/Host/TrustedDefaultSecurityEvaluator.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Runtime.Agent.Security;

namespace AefPlatform.Host;

// GA-3 Capability 1's trusted-default implementation of the security seam:
// every future adapter must call ISecurityEvaluator before submitting an
// ExecutionRequest (ADR-0025 Decision 3 - adapters translate and invoke
// the seam, never their own policy logic). Real authentication/
// authorization policy is a future capability's concern; this default
// represents a trusted local environment by authorizing everything,
// mirroring PermissiveActivationPolicyResolver's own precedent for the
// Kernel's activation-policy seam. Logs once at Debug level so a local
// run's logs make it obvious no real authorization is in effect (design
// Decision 3 - standardize on the existing ILogger<T> pattern, no new
// logging abstraction).
public sealed class TrustedDefaultSecurityEvaluator : ISecurityEvaluator
{
    private readonly ILogger<TrustedDefaultSecurityEvaluator> _logger;

    public TrustedDefaultSecurityEvaluator(ILogger<TrustedDefaultSecurityEvaluator> logger)
    {
        _logger = logger;
        _logger.LogDebug("Trusted-default security evaluator active - all requests are authorized.");
    }

    public SecurityDecision Evaluate(SecurityContext context) => new(IsAllowed: true, DenialReason: null);
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj --filter "FullyQualifiedName~TrustedDefaultSecurityEvaluatorTests"`
Expected: all three tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Runtime.Agent/Security/SecurityContext.cs src/Runtime.Agent/Security/SecurityDecision.cs src/Runtime.Agent/Security/ISecurityEvaluator.cs src/Host/TrustedDefaultSecurityEvaluator.cs tests/Host.Tests/TrustedDefaultSecurityEvaluatorTests.cs
git commit -m "feat(host): add the security seam (ISecurityEvaluator) and its trusted-default implementation"
```

---

### Task 4: Wire the security seam into the Host's DI graph, prove resolution and substitutability

**Files:**
- Modify: `src/Host/Program.cs`
- Test: `tests/Host.Tests/GA3Capability1RuntimeHostTests.cs` (create)

**Interfaces:**
- Consumes: `Runtime.Agent.Security.ISecurityEvaluator`, `AefPlatform.Host.TrustedDefaultSecurityEvaluator` (from Task 3).
- Produces: `ISecurityEvaluator` resolvable from the live Host's `IServiceProvider`, registered as a singleton, substitutable by any later registration of the same interface (standard DI "last registration wins" behavior — no new abstraction needed).

- [ ] **Step 1: Write the failing test**

Create `tests/Host.Tests/GA3Capability1RuntimeHostTests.cs`. This mirrors `GA2AgentExecutionHostTests.cs`'s own convention of duplicating `Program.cs`'s DI wiring in a test-local builder (there is no way to invoke top-level-statement `Program.cs` directly from a test):

```csharp
using AefPlatform.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Runtime.Agent;
using Runtime.Agent.Capabilities;
using Runtime.Agent.Security;
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

// GA-3 Capability 1: proves the security seam resolves from the real
// composition graph with the trusted-default implementation, and that it
// is genuinely substitutable via DI without touching Runtime.Agent or
// Program.cs - the Security Seam Validated gate (GA-3 engineering program
// doc, Section 9) requires demonstrating this, not merely asserting it.
public class GA3Capability1RuntimeHostTests
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
        builder.Services.AddSingleton<ISecurityEvaluator, TrustedDefaultSecurityEvaluator>();
        builder.Services.AddHostedService<RuntimeStartupHostedService>();

        return builder;
    }

    private static IDictionary<string, string?> DefaultConfig() => new Dictionary<string, string?>
    {
        ["AefPlatform:PlatformName"] = "AEF Platform Runtime",
        ["Ai:Provider"] = "OpenAI",
        ["Ai:OpenAi:ApiKey"] = "",
        ["Ai:OpenAi:Model"] = "gpt-4o",
    };

    [Fact]
    public async Task StartAsync_ResolvesTrustedDefaultSecurityEvaluator()
    {
        var builder = CreateBuilder(DefaultConfig());
        using var host = builder.Build();

        await host.StartAsync();
        var evaluator = host.Services.GetRequiredService<ISecurityEvaluator>();

        Assert.IsType<TrustedDefaultSecurityEvaluator>(evaluator);
        Assert.True(evaluator.Evaluate(new SecurityContext(null, null)).IsAllowed);
        await host.StopAsync();
    }

    private sealed class DenyAllSecurityEvaluator : ISecurityEvaluator
    {
        public SecurityDecision Evaluate(SecurityContext context) => new(IsAllowed: false, DenialReason: "Denied by test double.");
    }

    [Fact]
    public async Task StartAsync_SecurityEvaluatorIsSubstitutableViaDI()
    {
        var builder = CreateBuilder(DefaultConfig());
        builder.Services.AddSingleton<ISecurityEvaluator, DenyAllSecurityEvaluator>();
        using var host = builder.Build();

        await host.StartAsync();
        var evaluator = host.Services.GetRequiredService<ISecurityEvaluator>();

        Assert.IsType<DenyAllSecurityEvaluator>(evaluator);
        var decision = evaluator.Evaluate(new SecurityContext("caller-1", null));
        Assert.False(decision.IsAllowed);
        Assert.Equal("Denied by test double.", decision.DenialReason);
        await host.StopAsync();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails to compile (`Program.cs` hasn't registered `ISecurityEvaluator` yet — but note this test file's own `CreateBuilder` already registers it, so this step actually verifies the test file itself compiles and both tests pass against the test-local builder)**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj --filter "FullyQualifiedName~GA3Capability1RuntimeHostTests"`
Expected: PASS — this test's `CreateBuilder` is self-contained and doesn't depend on `Program.cs` yet. This step exists to confirm the test itself is correct before Step 3 brings `Program.cs` in sync with it.

- [ ] **Step 3: Update `Program.cs` to match — register the security seam in the real composition root**

In `src/Host/Program.cs`, add a using statement. Find:

```csharp
using Runtime.Agent;
using Runtime.Agent.Capabilities;
using Runtime.Agent.Sessions;
```

Replace with:

```csharp
using Runtime.Agent;
using Runtime.Agent.Capabilities;
using Runtime.Agent.Security;
using Runtime.Agent.Sessions;
```

Then find:

```csharp
builder.Services.AddSingleton<IAgentExecutionInterface>(sp => new AgentExecutionService(
    sp.GetRequiredService<EngineeringExecutionPipeline>(),
    sp.GetRequiredService<ICapabilityCatalog>(),
    sp.GetRequiredService<ISessionStore>()));
builder.Services.AddHostedService<RuntimeStartupHostedService>();
```

Replace with:

```csharp
builder.Services.AddSingleton<IAgentExecutionInterface>(sp => new AgentExecutionService(
    sp.GetRequiredService<EngineeringExecutionPipeline>(),
    sp.GetRequiredService<ICapabilityCatalog>(),
    sp.GetRequiredService<ISessionStore>()));
builder.Services.AddSingleton<ISecurityEvaluator, TrustedDefaultSecurityEvaluator>();
builder.Services.AddHostedService<RuntimeStartupHostedService>();
```

- [ ] **Step 4: Build the whole solution to confirm `Program.cs` still compiles**

Run: `dotnet build AefPlatform.sln`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 5: Re-run the Task 4 tests plus Task 3's tests to confirm nothing regressed**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj --filter "FullyQualifiedName~GA3Capability1RuntimeHostTests|FullyQualifiedName~TrustedDefaultSecurityEvaluatorTests"`
Expected: all 5 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Host/Program.cs tests/Host.Tests/GA3Capability1RuntimeHostTests.cs
git commit -m "feat(host): register the trusted-default security evaluator in the composition root"
```

---

### Task 5: Full regression run and documentation

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: nothing new.
- Produces: nothing new — this task is verification and documentation only.

- [ ] **Step 1: Run the full existing test suite**

Run: `dotnet test AefPlatform.sln`
Expected: all tests pass (324+ pre-existing tests, plus the 2 new tests in `InMemoryEvidenceSinkTests`, 6 in `InMemorySessionStoreTests`, 3 in `TrustedDefaultSecurityEvaluatorTests`, 2 in `GA3Capability1RuntimeHostTests` — 337+ total), 0 failures.

- [ ] **Step 2: Update `README.md`'s Status section**

In `README.md`, find the line starting with:

```
**AEF v1.0 GA-2 (AI Agent Integration Layer) complete, 2026-07-08.**
```

Add a new paragraph immediately after that entire paragraph (after its closing sentence "...no new NuGet dependency."):

```markdown

**AEF v1.0 GA-3 Capability 1 (AEF Runtime Host) complete, 2026-07-08.** First Capability of the External Execution Channels program — see `docs/superpowers/specs/2026-07-08-ga3-external-execution-channels-engineering-program.md` for the full GA-3 architecture and `docs/superpowers/specs/2026-07-08-ga3-capability1-runtime-host-design.md` for this Capability's design. Adapters attach to the Host as plain `IHostedService` registrations — no new hosting abstraction was introduced (none was needed for a single, not-yet-existing adapter). Adds the security seam GA-3 charters to this Capability: `Runtime.Agent.Security.ISecurityEvaluator`/`SecurityContext`/`SecurityDecision` (new, additive files — no existing GA-2 contract changed), with `TrustedDefaultSecurityEvaluator` registered in `Host`'s DI graph and proven substitutable via a DI-override test, closing GA-3's Security Seam Validated release gate. Also closes this Capability's concurrency-audit completion gate: two real races found in the existing DI graph are fixed — `InMemoryEvidenceSink`'s `List<EvidenceRecord>` is now lock-guarded against concurrent writes, and `InMemorySessionStore.Touch` now updates atomically via `ConcurrentDictionary.AddOrUpdate` instead of a non-atomic read-then-write. Health/diagnostics is explicitly deferred to Capability 4, per the GA-3 program document's own chartering (an Independent Engineering Review finding on this Capability's design). No `Runtime.Ai`, `Runtime.Agent`'s existing contracts, or `EngineeringExecutionPipeline` change; no new NuGet dependency.
```

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: record GA-3 Capability 1 (AEF Runtime Host) completion"
```

- [ ] **Step 4: Push the branch and confirm CI (if configured) is green**

Run: `git push`
Expected: push succeeds; if the repo has CI configured on this branch/PR, confirm it goes green before requesting the Engineering Review merge gate.
