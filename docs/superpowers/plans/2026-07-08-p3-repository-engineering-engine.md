# P3 — Repository Engineering Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the P2 `EngineeringExecutionPipeline` into a real repository engineering engine — able to discover a repository's context, read/write/mutate its files and git state, analyze its technology/structure, plan work against it, validate the outcome, and return a structured result — by closing the genuine runtime gaps against the already-`Active`, already-governed `AGT-EXE-0012` (Engineering Planner) and `AGT-EXE-0015` (Repository Intelligence Agent) specs, reusing every existing Integration Adapter (`IRepositoryScanner`, `StackDetector`, `IIsolatedChangeVerifier`, `IVerificationRunner`) wherever it already does the job.

**Architecture:** Two new Integration Adapters close the only genuine capability gaps below the Skill layer: `IRepositoryFileOperations` (file/directory CRUD, root-bounded like the existing scanner) and `IGitOperations` (status/diff/branch-create/commit/merge-readiness, alongside the existing narrower `IIsolatedChangeVerifier`). A new `RepositoryExecutionContext` composes the existing scanner + `StackDetector` + the new `IGitOperations` into one Repository Context object (root, name, branch, working-tree state, Technology Profile) — the same composition pattern `RepositoryOnboardingAssessmentAgent` already established for `AGT-EXE-0007`. Seven new `IBehavior` realizations — `SKL-EXE-0023/0024/0025` (`AGT-EXE-0012`, Engineering Planner) and `SKL-EXE-0030/0031/0032/0033` (`AGT-EXE-0015`, Repository Intelligence) — are deterministic, evidence-grounded realizations of their governed Prompt Contracts, exactly as `SKL-EXE-0021`'s deterministic keyword classifier already precedents: no LLM integration exists anywhere in this Runtime, so every judgment a Skill's governed spec expects free-text/NL reasoning for is instead honestly reported as `UNKNOWN`/an Open Item/a low confidence score with justification — never fabricated. `EngineeringExecutionPipeline` gains two new sequential dispatch chains (Planning: 0023→0024→0025; Repository Intelligence: 0030→0031→0032→0033) alongside its existing Feature/Epic/Delivery chains, plus a Repository Validation dispatch reusing `DeliveryValidationBehavior`'s already-generic `IVerificationRunner` machinery through a new `BuildSystemDetector` (stack → default build/test command). `ExecutionResult` gains three additive, nullable fields (`RepositoryAnalyzed`, `FilesChanged`, `TestsExecuted`) — `null` for every existing non-repository dispatch path, so no existing test's assertions change.

**Tech Stack:** unchanged from P1/P2 — .NET 8, xUnit 2.5.3 / Microsoft.NET.Test.Sdk 17.8.0.

## Global Constraints

- Reuse first, extend second, create new only when the capability genuinely does not exist. Do not rename, replace, or redesign `IRegistry`, `RuntimeCatalog`, `AgentHost`/`SkillHost`/`StrategyHost`, `FeatureLifecycleOrchestrator`/`EpicLifecycleOrchestrator`, `RuntimeExecutionContext`/`ContextSeed`, `RuntimeBootstrapper`, `IRepositoryScanner`/`LocalFileSystemRepositoryScanner`/`StackDetector`, `IIsolatedChangeVerifier`/`LocalGitChangeVerifier`, `IVerificationRunner`/`ProcessVerificationRunner`, `RepositoryAssessmentBehavior`/`RepositoryOnboardingAssessmentAgent`, `TechnologyProfileDraft`, `ConversationIntakeAndRoutingBehavior`'s existing keyword table entries, or any existing `ExecutionResult`/`EngineeringExecutionPipeline` call site's existing behavior.
- `IBehavior.Execute(RuntimeExecutionContext)` stays exactly as defined — every new Behavior reads its per-call input from `context.Tier3SkillContent`, the same extension point every P2 Behavior already uses.
- No governed Skill here has any deterministic-algorithm precedent — each is specified as free-text/NL interpretation (mirroring `SKL-EXE-0021`'s own documented rationale). Every new Behavior must honestly report `UNKNOWN` / an Open Item / a low, justified confidence score whenever its governed Contract expects real NL judgment this Runtime cannot perform — it must never fabricate a finding, a specification section's content, or a root-cause conclusion. This is not a shortfall to hide; it is the correct, non-hollow realization of these Contracts' own escalation semantics.
- New Integration Adapters (`IRepositoryFileOperations`, `IGitOperations`) are root-bounded exactly like `LocalFileSystemRepositoryScanner` — no member accepts a network address or credential; every git invocation uses `ProcessStartInfo.ArgumentList`, never a concatenated command-line string (the same discipline `LocalGitChangeVerifier`/`ProcessVerificationRunner` already apply).
- Every new/changed `.csproj` stays net8.0, `Nullable`/`ImplicitUsings` enabled, matching existing conventions. No new NuGet dependency is introduced — everything here is achievable with `System.Diagnostics.Process` and `System.IO`, exactly like every existing Integration Adapter.
- Whole solution (`AefPlatform.sln`) must build and all tests pass after every commit.
- No placeholder implementations, no TODOs. `Date`/timestamp fields use `DateTime.UtcNow` directly (this is plain C# runtime code, not a Workflow script — no restriction applies here).
- `AGT-EXE-0013`/`0014` (Specification Reviewer, Validation Agent) and their Skills remain explicitly out of scope for this Prompt — P3's mission is `AGT-EXE-0012` (Planner) and `AGT-EXE-0015` (Repository Intelligence) only, per the Prompt's own §5 (Planner Integration) and §4 (Repository Intelligence) scope.

## Gap Analysis

| Capability | Existing Implementation | Gap | Action |
|---|---|---|---|
| Repository discovery / metadata / execution context | `LocalFileSystemRepositoryScanner` (readability, bounded listing), `StackDetector` (technology inventory) — both read-only, no branch/working-tree concept | No unified Repository Context (branch, working-tree state, commit) exists | **Implement** `RepositoryExecutionContext` + `RepositoryContextFactory`, composing the existing scanner/detector with the new `IGitOperations` |
| Branch information / working tree status | `IIsolatedChangeVerifier.BranchExists`/`IsDefaultBranch` only — no current-branch query, no working-tree-clean check | Narrow, verification-only surface; no status/diff | **Implement** `IGitOperations.GetStatus()` |
| Read files | `IRepositoryScanner.ListEntries` lists names only — no member reads file content | Genuine gap | **Implement** `IRepositoryFileOperations.ReadFile` |
| Write / create / delete / move files, directory operations | None anywhere in the Runtime | Genuine gap | **Implement** `IRepositoryFileOperations` (`WriteFile`, `CreateDirectory`, `DeleteFile`, `DeleteDirectory`, `Move`) |
| Git: repository detection, status, diff, branch creation, commit creation, merge readiness | `LocalGitChangeVerifier` covers `BranchExists`/`IsDefaultBranch`/`CommitExistsOnBranch` only (Story S30's narrow verification scope) | Genuine gap for every other git operation named in the Prompt | **Implement** `IGitOperations` (`IsGitRepository`, `GetStatus`, `GetDiff`, `CreateBranch`, `Commit`, `CheckMergeReadiness`); `IIsolatedChangeVerifier`/`LocalGitChangeVerifier` stay untouched |
| Technology identification | `StackDetector.DetectTechnologies` (dotnet/node/python/java/rust/go marker detection) — complete, evidenced, bounded | None | **Reuse** verbatim |
| Project structure discovery | `IRepositoryScanner.ListEntries` (one directory at a time) | No aggregate "project structure" concept beyond what `StackDetector` already assembles | **Reuse**: `RepositoryExecutionContext`'s Technology Profile + on-demand `ListEntries` calls in `SKL-EXE-0030` satisfy this without a new abstraction |
| Dependency discovery | None — no file-content reading exists yet (the gap above) | Genuine gap, but reading a manifest's *parsed dependency list* (e.g. every `<PackageReference>`) is real NL/format-specific judgment beyond a marker-file check | **Implement** minimally: `SKL-EXE-0031`'s Code Intelligence signal set includes a real, mechanical "manifest present but unparsed — dependency list not extracted" `UNKNOWN`-with-rationale finding, not a fabricated dependency graph — full manifest parsing across six ecosystems is out of proportion to this Prompt and is documented as a remaining gap |
| Build system detection | `StackDetector` names a *stack* (`dotnet`/`node`/...), not a *build command* | Genuine gap: no stack→command mapping exists | **Implement** `BuildSystemDetector` (stack → default build/test command, e.g. `dotnet` → `dotnet test`) |
| Engineering Planner (`AGT-EXE-0012`, `SKL-EXE-0023/0024/0025`) | Zero code — explicitly deferred by P2's own README entry | Governed, `Active`, zero implementation | **Implement** three deterministic Behaviors realizing `PCT-EXE-0023/0024/0025` exactly (Discovery from real repo scan data; mechanical Specification-draft assembly with honest Open Items for every section requiring real judgment; Readiness classification from Open Items/Alignment Findings counts) |
| Repository Intelligence (`AGT-EXE-0015`, `SKL-EXE-0030/0031/0032/0033`) | Zero code | Governed, `Active`, zero implementation. Role explicitly states "Consume, never re-derive, the Technology/Project Profile `AGT-EXE-0007` already produced" | **Implement** four deterministic Behaviors realizing `PCT-EXE-0030/0031/0032/0033`, consuming `RepositoryExecutionContext`'s Technology Profile (reused, not re-derived) |
| Validation Pipeline (build/test execution, diagnostics collection) | `DeliveryValidationBehavior` + `IVerificationRunner`/`ProcessVerificationRunner` — already fully generic (`WorkingDirectory`, `Command`, `Arguments` → exit code + stdout/stderr excerpt), not Delivery-specific in shape | None beyond deriving the `Command`/`Arguments` from a detected build system | **Reuse** `IVerificationRunner`/`ProcessVerificationRunner`/`ValidationRequest`/`ValidationOutcome` verbatim; **Implement** only the thin `RepositoryValidationSkillBehavior` adapter + `BuildSystemDetector` |
| Engineering Result (repository analyzed, files changed, tests executed) | `ExecutionResult` already carries `Diagnostics`/`ValidationResults`/`PresentedResult`/`SuggestedNextStep` | No repository-specific fields | **Extend** `ExecutionResult` with three additive nullable fields, populated only on repository-engineering dispatch paths |
| Runtime Integration (Conversation Manager → Orchestrator → Agent Dispatch → Skill Execution → Workflow Execution) | `EngineeringExecutionPipeline` already the single composition point; `ConversationIntakeAndRoutingBehavior` already the single routing point | New capabilities need new routing keywords + new `Dispatch` cases — no new pipeline | **Extend** both, in place; no parallel execution path |
| `WFL-DEL-0006`/`WFL-DEL-0008` (Engineering Planning / Repository Intelligence Workflows) | Both are Skill-sequence Workflows (no `StrategyHost`/Orchestrator state machine — confirmed by reading both Workflow docs), structurally identical in shape to the existing Delivery Execution dispatch (`SKL-EXE-0018` direct `SkillHost` invocation) | No Orchestrator wiring needed | **Reuse** the existing direct-`SkillHost`-dispatch pattern `FromDeliveryExecution` already establishes — no new Orchestrator class |
| Automated Tests | N/A | New capabilities need coverage | **Implement** |

## Architecture Decisions (implementation-only)

1. **`IGitOperations` is a new, broader adapter alongside the existing, narrower `IIsolatedChangeVerifier`** — not an extension of it. `IIsolatedChangeVerifier`'s own doc comment scopes it deliberately to isolated-change *verification* (Story S30); status/diff/branch-create/commit/merge-readiness are a different, broader concern serving Repository Operations generally. Duplicating a small `RunGit` process-invocation helper mirrors the codebase's own established pattern (`LocalGitChangeVerifier` and `ProcessVerificationRunner` already each have their own, independent copy) rather than introducing a shared base class no existing adapter uses.
2. **Merge readiness is evidence-only, never a mutating dry-run.** `CheckMergeReadiness` reports `BranchExists && !IsDefaultBranch && WorkingTreeClean` — it never invokes `git merge`/`git merge-tree` against the real repository, even with `--no-commit`/`--abort`, because a mutating check inside a "validation" call is exactly the kind of hidden side effect this codebase's own discipline (structured failure, never surprise) forbids. True three-way conflict prediction is documented as a known, out-of-scope limitation, the same way `StackDetector`'s own doc comment documents its known limitations.
3. **Every Repository Intelligence/Planner Behavior honestly reports the limits of deterministic execution** rather than fabricating NL output — `SKL-EXE-0031`'s complexity/duplication/dead-code signals are `UNKNOWN` with rationale (the Agent's own Role #3 explicitly sanctions this: "`UNKNOWN`-with-rationale otherwise"); `SKL-EXE-0032`'s root-cause confidence score is fixed low with an honest justification and one alternative hypothesis per evidence source (never a fabricated causal chain); `SKL-EXE-0024`'s Specification Draft populates every Always-required `SCH-DEL-0003` section with either real, scan-derived content or an explicit statement recorded in `open_items` — never silently blank, never invented.
4. **`RepositoryExecutionContext` is a new, additive composition type**, distinct from both `RuntimeExecutionContext` (the four-tier per-instance concept) and `EngineeringExecutionContext` (P2's session-level concept) — it represents one target repository's assessed state (root, name, branch, working-tree cleanliness, Technology Profile), reusing `TechnologyProfileDraft` verbatim as its Technology Profile field, per `AGT-EXE-0015`'s own Role #1 ("consume, never re-derive").
5. **`ExecutionResult`'s three new fields are additive and nullable** — every existing Feature/Epic/Delivery dispatch path constructs `ExecutionResult` exactly as it does today (positional records — the new fields are appended last, so every existing call site compiles unchanged only if given explicit values; all existing construction sites are updated in Task 13 to pass `null` for the three new fields, which is a mechanical, non-behavioral edit).
6. **No Orchestrator/`StrategyHost` involvement for Planning or Repository Intelligence dispatch** — both `WFL-DEL-0006` and `WFL-DEL-0008` are confirmed (by reading both governed Workflow docs) to be plain sequential Skill invocations with only a final Human-Approval-style pause, not multi-session state machines. `EngineeringExecutionPipeline.Dispatch` invokes each Skill in sequence directly through the existing `SkillHost`, exactly as `FromDeliveryExecution` already does for `SKL-EXE-0018`.

## File Structure

- `src/Runtime/Integration/RepositoryOperations/IRepositoryFileOperations.cs` — **create**.
- `src/Runtime/Integration/RepositoryOperations/RepositoryFileReadResult.cs` — **create**.
- `src/Runtime/Integration/RepositoryOperations/RepositoryOperationResult.cs` — **create**.
- `src/Runtime/Integration/RepositoryOperations/LocalFileSystemRepositoryOperations.cs` — **create**.
- `src/Runtime/Integration/GitOperations/IGitOperations.cs` — **create**.
- `src/Runtime/Integration/GitOperations/GitStatusResult.cs` — **create**.
- `src/Runtime/Integration/GitOperations/GitOperationResult.cs` — **create**.
- `src/Runtime/Integration/GitOperations/MergeReadinessResult.cs` — **create**.
- `src/Runtime/Integration/GitOperations/LocalGitOperations.cs` — **create**.
- `src/Runtime/Execution/RepositoryExecutionContext.cs` — **create**.
- `src/Runtime/Execution/RepositoryContextFactory.cs` — **create**.
- `src/Host/EngineeringArchitectureManifest.cs` — **modify**: +11 entries (35 total).
- `src/Runtime/DomainContent/RepositoryIntelligence/RepositoryIntelligenceTypes.cs` — **create** (all four Skills' request/result records).
- `src/Runtime/DomainContent/RepositoryIntelligence/RepositoryAndArchitectureUnderstandingBehavior.cs` — **create** (SKL-EXE-0030).
- `src/Runtime/DomainContent/RepositoryIntelligence/CodeIntelligenceAndQualitySignalDetectionBehavior.cs` — **create** (SKL-EXE-0031).
- `src/Runtime/DomainContent/RepositoryIntelligence/EngineeringDiagnosticsAndRootCauseAnalysisBehavior.cs` — **create** (SKL-EXE-0032).
- `src/Runtime/DomainContent/RepositoryIntelligence/RecommendationSynthesisAndEvidenceBasedReportingBehavior.cs` — **create** (SKL-EXE-0033).
- `src/Runtime/DomainContent/EngineeringPlanning/EngineeringPlanningTypes.cs` — **create** (all three Skills' request/result records).
- `src/Runtime/DomainContent/EngineeringPlanning/EngineeringDiscoveryAndGoalClarificationBehavior.cs` — **create** (SKL-EXE-0023).
- `src/Runtime/DomainContent/EngineeringPlanning/SpecificationDraftingAndAlignmentBehavior.cs` — **create** (SKL-EXE-0024).
- `src/Runtime/DomainContent/EngineeringPlanning/ReadinessClassificationAndApprovalRequestBehavior.cs` — **create** (SKL-EXE-0025).
- `src/Runtime/DomainContent/RepositoryValidation/BuildSystemDetector.cs` — **create**.
- `src/Runtime/DomainContent/RepositoryValidation/RepositoryValidationSkillBehavior.cs` — **create**.
- `src/Runtime/Execution/ExecutionResult.cs` — **modify**: +3 fields.
- `src/Runtime/Execution/EngineeringExecutionPipeline.cs` — **modify**: 3 new dispatch branches, existing branches updated for the new `ExecutionResult` shape.
- `src/Runtime/DomainContent/ConversationManagement/ConversationIntakeAndRoutingBehavior.cs` — **modify**: +3 keyword→capability entries.
- `src/Host/Program.cs` — **modify**: register 7 new Behaviors.
- `tests/Runtime.Tests/Integration/RepositoryOperations/LocalFileSystemRepositoryOperationsTests.cs` — **create**.
- `tests/Runtime.Tests/Integration/GitOperations/LocalGitOperationsTests.cs` — **create**.
- `tests/Runtime.Tests/Execution/RepositoryContextFactoryTests.cs` — **create**.
- `tests/Host.Tests/RuntimeArchitectureLoaderTests.cs` — **modify**: manifest count 24→35.
- `tests/Runtime.Tests/DomainContent/RepositoryIntelligence/*.cs` — **create** (4 test files).
- `tests/Runtime.Tests/DomainContent/EngineeringPlanning/*.cs` — **create** (3 test files).
- `tests/Runtime.Tests/DomainContent/RepositoryValidation/BuildSystemDetectorTests.cs`, `RepositoryValidationSkillBehaviorTests.cs` — **create**.
- `tests/Runtime.Tests/Execution/EngineeringExecutionPipelineTests.cs` — **modify**: existing `ExecutionResult` constructions updated; new dispatch-path tests added.
- `tests/Host.Tests/RepositoryEngineeringHostIntegrationTests.cs` — **create** (live-Host end-to-end proof).
- `README.md` — **modify**: P3 status entry.

---

## Task 1: Repository Operations Integration Adapter

**Files:** Create `src/Runtime/Integration/RepositoryOperations/IRepositoryFileOperations.cs`, `RepositoryFileReadResult.cs`, `RepositoryOperationResult.cs`, `LocalFileSystemRepositoryOperations.cs`; create `tests/Runtime.Tests/Integration/RepositoryOperations/LocalFileSystemRepositoryOperationsTests.cs`.

**Interfaces:** New adapter contract, root-bounded exactly like `IRepositoryScanner`/`LocalFileSystemRepositoryScanner` (unmodified, untouched by this task).

```csharp
// IRepositoryFileOperations.cs
namespace Runtime.Integration.RepositoryOperations;

// The Repository Operations Integration Adapter Boundary contract (P3 §2):
// read/write/create/delete/move a file or directory, root-bounded exactly
// like IRepositoryScanner - no member accepts a network address,
// credential, or process-execution argument.
public interface IRepositoryFileOperations
{
    RepositoryFileReadResult ReadFile(string relativePath);

    RepositoryOperationResult WriteFile(string relativePath, string content);

    RepositoryOperationResult CreateDirectory(string relativePath);

    RepositoryOperationResult DeleteFile(string relativePath);

    RepositoryOperationResult DeleteDirectory(string relativePath, bool recursive);

    RepositoryOperationResult Move(string sourceRelativePath, string destinationRelativePath);
}
```

```csharp
// RepositoryFileReadResult.cs
namespace Runtime.Integration.RepositoryOperations;

public enum RepositoryFileReadStatus
{
    Success,
    RootNotReadable,
    PathEscapesRoot,
    FileNotFound,
    ReadFailed,
}

// Either the real file content, or a structured failure reason - never an
// unhandled exception (mirrors RepositoryScanResult's own shape).
public sealed record RepositoryFileReadResult(RepositoryFileReadStatus Status, string? Content, string? FailureReason)
{
    public static RepositoryFileReadResult Success(string content) => new(RepositoryFileReadStatus.Success, content, null);

    public static RepositoryFileReadResult Failure(RepositoryFileReadStatus status, string reason) => new(status, null, reason);
}
```

```csharp
// RepositoryOperationResult.cs
namespace Runtime.Integration.RepositoryOperations;

public enum RepositoryOperationStatus
{
    Success,
    RootNotReadable,
    PathEscapesRoot,
    PathNotFound,
    OperationFailed,
}

// The uniform structured-result shape for every mutating Repository
// Operation - never an unhandled exception, never a silent no-op.
public sealed record RepositoryOperationResult(RepositoryOperationStatus Status, string? FailureReason)
{
    public static readonly RepositoryOperationResult Ok = new(RepositoryOperationStatus.Success, null);

    public static RepositoryOperationResult Failure(RepositoryOperationStatus status, string reason) => new(status, reason);
}
```

```csharp
// LocalFileSystemRepositoryOperations.cs
namespace Runtime.Integration.RepositoryOperations;

// The one concrete binding of IRepositoryFileOperations in scope: local
// filesystem, root-bounded. Resolution/escape-prevention mirrors
// LocalFileSystemRepositoryScanner's own algorithm (Epic 0008, Story S25) -
// duplicated rather than shared, consistent with this codebase's existing
// pattern of independently self-contained Integration Adapters (S22's
// scanner and Story S30's git verifier already never share code either).
public sealed class LocalFileSystemRepositoryOperations : IRepositoryFileOperations
{
    private readonly string _root;

    public LocalFileSystemRepositoryOperations(string root)
    {
        if (string.IsNullOrEmpty(root))
        {
            throw new ArgumentException("Root path must not be null or empty; the adapter has no boundary to enforce without one.", nameof(root));
        }

        _root = Path.GetFullPath(root);
    }

    public RepositoryFileReadResult ReadFile(string relativePath)
    {
        var resolved = Resolve(relativePath);
        if (resolved is null)
        {
            return RepositoryFileReadResult.Failure(RepositoryFileReadStatus.PathEscapesRoot, $"Path '{relativePath}' resolves outside root '{_root}'.");
        }

        if (!File.Exists(resolved))
        {
            return RepositoryFileReadResult.Failure(RepositoryFileReadStatus.FileNotFound, $"File '{relativePath}' does not exist within root '{_root}'.");
        }

        try
        {
            return RepositoryFileReadResult.Success(File.ReadAllText(resolved));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return RepositoryFileReadResult.Failure(RepositoryFileReadStatus.ReadFailed, $"File '{relativePath}' could not be read: {ex.Message}");
        }
    }

    public RepositoryOperationResult WriteFile(string relativePath, string content)
    {
        var resolved = Resolve(relativePath);
        if (resolved is null)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathEscapesRoot, $"Path '{relativePath}' resolves outside root '{_root}'.");
        }

        try
        {
            var parent = Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllText(resolved, content);
            return RepositoryOperationResult.Ok;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.OperationFailed, $"File '{relativePath}' could not be written: {ex.Message}");
        }
    }

    public RepositoryOperationResult CreateDirectory(string relativePath)
    {
        var resolved = Resolve(relativePath);
        if (resolved is null)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathEscapesRoot, $"Path '{relativePath}' resolves outside root '{_root}'.");
        }

        try
        {
            Directory.CreateDirectory(resolved);
            return RepositoryOperationResult.Ok;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.OperationFailed, $"Directory '{relativePath}' could not be created: {ex.Message}");
        }
    }

    public RepositoryOperationResult DeleteFile(string relativePath)
    {
        var resolved = Resolve(relativePath);
        if (resolved is null)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathEscapesRoot, $"Path '{relativePath}' resolves outside root '{_root}'.");
        }

        if (!File.Exists(resolved))
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathNotFound, $"File '{relativePath}' does not exist within root '{_root}'.");
        }

        try
        {
            File.Delete(resolved);
            return RepositoryOperationResult.Ok;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.OperationFailed, $"File '{relativePath}' could not be deleted: {ex.Message}");
        }
    }

    public RepositoryOperationResult DeleteDirectory(string relativePath, bool recursive)
    {
        var resolved = Resolve(relativePath);
        if (resolved is null)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathEscapesRoot, $"Path '{relativePath}' resolves outside root '{_root}'.");
        }

        if (!Directory.Exists(resolved))
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathNotFound, $"Directory '{relativePath}' does not exist within root '{_root}'.");
        }

        try
        {
            Directory.Delete(resolved, recursive);
            return RepositoryOperationResult.Ok;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.OperationFailed, $"Directory '{relativePath}' could not be deleted: {ex.Message}");
        }
    }

    public RepositoryOperationResult Move(string sourceRelativePath, string destinationRelativePath)
    {
        var source = Resolve(sourceRelativePath);
        var destination = Resolve(destinationRelativePath);
        if (source is null || destination is null)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathEscapesRoot, $"Source '{sourceRelativePath}' or destination '{destinationRelativePath}' resolves outside root '{_root}'.");
        }

        var sourceIsFile = File.Exists(source);
        var sourceIsDirectory = Directory.Exists(source);
        if (!sourceIsFile && !sourceIsDirectory)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathNotFound, $"Source '{sourceRelativePath}' does not exist within root '{_root}'.");
        }

        try
        {
            var destinationParent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationParent))
            {
                Directory.CreateDirectory(destinationParent);
            }

            if (sourceIsFile)
            {
                File.Move(source, destination);
            }
            else
            {
                Directory.Move(source, destination);
            }

            return RepositoryOperationResult.Ok;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.OperationFailed, $"Move from '{sourceRelativePath}' to '{destinationRelativePath}' failed: {ex.Message}");
        }
    }

    private string? Resolve(string relativePath)
    {
        var combined = Path.GetFullPath(Path.Combine(_root, relativePath ?? string.Empty));
        var normalizedRoot = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedCandidate = combined.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var withinRoot = normalizedCandidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        return withinRoot ? combined : null;
    }
}
```

Test (`tests/Runtime.Tests/Integration/RepositoryOperations/LocalFileSystemRepositoryOperationsTests.cs`) — use a real temp directory (created/torn down per test via `Directory.CreateTempSubdirectory()`/`Directory.Delete(path, recursive: true)` in a `try`/`finally`), mirroring `LocalFileSystemRepositoryScannerTests`' own real-filesystem style:

```csharp
using Runtime.Integration.RepositoryOperations;

namespace Runtime.Tests.Integration.RepositoryOperations;

public class LocalFileSystemRepositoryOperationsTests
{
    [Fact]
    public void WriteFile_ThenReadFile_RoundTripsContent()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var ops = new LocalFileSystemRepositoryOperations(root.FullName);

            var writeResult = ops.WriteFile("src/hello.txt", "hello world");
            var readResult = ops.ReadFile("src/hello.txt");

            Assert.Equal(RepositoryOperationStatus.Success, writeResult.Status);
            Assert.Equal(RepositoryFileReadStatus.Success, readResult.Status);
            Assert.Equal("hello world", readResult.Content);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void ReadFile_MissingFile_ReturnsFileNotFound()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var ops = new LocalFileSystemRepositoryOperations(root.FullName);

            var result = ops.ReadFile("does-not-exist.txt");

            Assert.Equal(RepositoryFileReadStatus.FileNotFound, result.Status);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void ReadFile_PathEscapesRoot_ReturnsPathEscapesRoot()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var ops = new LocalFileSystemRepositoryOperations(root.FullName);

            var result = ops.ReadFile("../outside.txt");

            Assert.Equal(RepositoryFileReadStatus.PathEscapesRoot, result.Status);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void DeleteFile_ExistingFile_RemovesIt()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var ops = new LocalFileSystemRepositoryOperations(root.FullName);
            ops.WriteFile("temp.txt", "x");

            var result = ops.DeleteFile("temp.txt");

            Assert.Equal(RepositoryOperationStatus.Success, result.Status);
            Assert.False(File.Exists(Path.Combine(root.FullName, "temp.txt")));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void Move_ExistingFile_RelocatesIt()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var ops = new LocalFileSystemRepositoryOperations(root.FullName);
            ops.WriteFile("a.txt", "content");

            var result = ops.Move("a.txt", "moved/b.txt");

            Assert.Equal(RepositoryOperationStatus.Success, result.Status);
            Assert.False(File.Exists(Path.Combine(root.FullName, "a.txt")));
            Assert.True(File.Exists(Path.Combine(root.FullName, "moved", "b.txt")));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void CreateDirectory_ThenDeleteDirectory_Succeeds()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var ops = new LocalFileSystemRepositoryOperations(root.FullName);

            var createResult = ops.CreateDirectory("nested/dir");
            var deleteResult = ops.DeleteDirectory("nested", recursive: true);

            Assert.Equal(RepositoryOperationStatus.Success, createResult.Status);
            Assert.Equal(RepositoryOperationStatus.Success, deleteResult.Status);
            Assert.False(Directory.Exists(Path.Combine(root.FullName, "nested")));
        }
        finally
        {
            root.Delete(true);
        }
    }
}
```

- [ ] Write the failing tests, confirm compile failure (`LocalFileSystemRepositoryOperations` doesn't exist).
- [ ] Implement all four files above.
- [ ] `dotnet test tests/Runtime.Tests/Runtime.Tests.csproj` — new tests pass.
- [ ] Full suite green.
- [ ] Commit: `feat(runtime): implement the Repository Operations Integration Adapter (file/directory CRUD)`.

---

## Task 2: Git Operations Integration Adapter

**Files:** Create `src/Runtime/Integration/GitOperations/IGitOperations.cs`, `GitStatusResult.cs`, `GitOperationResult.cs`, `MergeReadinessResult.cs`, `LocalGitOperations.cs`; create `tests/Runtime.Tests/Integration/GitOperations/LocalGitOperationsTests.cs`.

```csharp
// IGitOperations.cs
namespace Runtime.Integration.GitOperations;

// The Git Operations Integration Adapter Boundary contract (P3 §3) - a
// broader surface than IIsolatedChangeVerifier's own deliberately narrow
// verification-only scope (Story S30). No member accepts a network
// address or credential; every implementation must invoke git via
// ProcessStartInfo.ArgumentList only.
public interface IGitOperations
{
    bool IsGitRepository();

    GitStatusResult GetStatus();

    string GetDiff();

    GitOperationResult CreateBranch(string branchName);

    GitOperationResult Commit(string message);

    MergeReadinessResult CheckMergeReadiness(string branchName, string defaultBranchName);
}
```

```csharp
// GitStatusResult.cs
namespace Runtime.Integration.GitOperations;

// The working-tree status Repository Context needs (P3 §1). IsClean is
// true only when neither staged, unstaged, nor untracked entries exist.
public sealed record GitStatusResult(
    string? CurrentBranch,
    bool IsClean,
    IReadOnlyList<string> ModifiedPaths,
    IReadOnlyList<string> UntrackedPaths);
```

```csharp
// GitOperationResult.cs
namespace Runtime.Integration.GitOperations;

public sealed record GitOperationResult(bool Success, string? FailureReason)
{
    public static readonly GitOperationResult Ok = new(true, null);

    public static GitOperationResult Failure(string reason) => new(false, reason);
}
```

```csharp
// MergeReadinessResult.cs
namespace Runtime.Integration.GitOperations;

// Evidence-only merge readiness (Architecture Decision 2): never a
// mutating dry-run merge. Reasons lists every unmet condition, never
// silently absent.
public sealed record MergeReadinessResult(bool IsReady, IReadOnlyList<string> Reasons);
```

```csharp
// LocalGitOperations.cs
using System.Diagnostics;

namespace Runtime.Integration.GitOperations;

// The one concrete binding of IGitOperations in scope: local, on-disk Git
// repository. Every git invocation uses ProcessStartInfo.ArgumentList,
// never a concatenated command-line string - the same injection-safety
// discipline LocalGitChangeVerifier/ProcessVerificationRunner already
// apply. A separate, independent adapter from IIsolatedChangeVerifier
// (Architecture Decision 1) - not a replacement, not a shared base class.
public sealed class LocalGitOperations : IGitOperations
{
    private readonly string _repositoryRoot;

    public LocalGitOperations(string repositoryRoot)
    {
        if (string.IsNullOrEmpty(repositoryRoot))
        {
            throw new ArgumentException("repositoryRoot must not be null or empty.", nameof(repositoryRoot));
        }

        _repositoryRoot = repositoryRoot;
    }

    public bool IsGitRepository() => RunGit("rev-parse", "--is-inside-work-tree").ExitCode == 0;

    public GitStatusResult GetStatus()
    {
        var branchResult = RunGit("rev-parse", "--abbrev-ref", "HEAD");
        var currentBranch = branchResult.ExitCode == 0 ? branchResult.StandardOutput.Trim() : null;

        var statusResult = RunGit("status", "--porcelain");
        var modified = new List<string>();
        var untracked = new List<string>();

        if (statusResult.ExitCode == 0)
        {
            foreach (var line in statusResult.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.Length < 4)
                {
                    continue;
                }

                var marker = trimmed[..2];
                var path = trimmed[3..];
                if (marker == "??")
                {
                    untracked.Add(path);
                }
                else
                {
                    modified.Add(path);
                }
            }
        }

        return new GitStatusResult(currentBranch, modified.Count == 0 && untracked.Count == 0, modified, untracked);
    }

    public string GetDiff()
    {
        var result = RunGit("diff");
        return result.ExitCode == 0 ? result.StandardOutput : string.Empty;
    }

    public GitOperationResult CreateBranch(string branchName)
    {
        if (string.IsNullOrEmpty(branchName))
        {
            return GitOperationResult.Failure("branchName must not be null or empty.");
        }

        var result = RunGit("branch", branchName);
        return result.ExitCode == 0 ? GitOperationResult.Ok : GitOperationResult.Failure(result.StandardError.Trim());
    }

    public GitOperationResult Commit(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return GitOperationResult.Failure("message must not be null or empty.");
        }

        var addResult = RunGit("add", "-A");
        if (addResult.ExitCode != 0)
        {
            return GitOperationResult.Failure(addResult.StandardError.Trim());
        }

        var commitResult = RunGit("commit", "-m", message);
        return commitResult.ExitCode == 0 ? GitOperationResult.Ok : GitOperationResult.Failure(commitResult.StandardError.Trim());
    }

    public MergeReadinessResult CheckMergeReadiness(string branchName, string defaultBranchName)
    {
        var reasons = new List<string>();

        var branchExists = RunGit("rev-parse", "--verify", "--quiet", $"refs/heads/{branchName}").ExitCode == 0;
        if (!branchExists)
        {
            reasons.Add($"Branch '{branchName}' does not exist.");
        }

        if (string.Equals(branchName, defaultBranchName, StringComparison.Ordinal))
        {
            reasons.Add($"'{branchName}' is the default branch - nothing to merge into itself.");
        }

        var status = GetStatus();
        if (!status.IsClean)
        {
            reasons.Add("The working tree has uncommitted changes.");
        }

        return new MergeReadinessResult(reasons.Count == 0, reasons);
    }

    private (int ExitCode, string StandardOutput, string StandardError) RunGit(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = _repositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the 'git' process - is Git installed and on PATH?");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, standardOutput, standardError);
    }
}
```

Test (`tests/Runtime.Tests/Integration/GitOperations/LocalGitOperationsTests.cs`) — initialize a real temp git repo per test (`git init`, configure a throwaway user, mirroring how a real repository behaves), teardown in `finally`:

```csharp
using System.Diagnostics;
using Runtime.Integration.GitOperations;

namespace Runtime.Tests.Integration.GitOperations;

public class LocalGitOperationsTests
{
    private static string InitRepository()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        RunGit(root, "init", "-b", "main");
        RunGit(root, "config", "user.email", "test@example.com");
        RunGit(root, "config", "user.name", "Test User");
        File.WriteAllText(Path.Combine(root, "README.md"), "# test repo");
        RunGit(root, "add", "-A");
        RunGit(root, "commit", "-m", "initial commit");
        return root;
    }

    private static void RunGit(string root, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in arguments) startInfo.ArgumentList.Add(arg);
        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
    }

    [Fact]
    public void IsGitRepository_RealRepository_ReturnsTrue()
    {
        var root = InitRepository();
        try
        {
            Assert.True(new LocalGitOperations(root).IsGitRepository());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetStatus_CleanRepository_ReportsCleanAndCurrentBranch()
    {
        var root = InitRepository();
        try
        {
            var status = new LocalGitOperations(root).GetStatus();

            Assert.Equal("main", status.CurrentBranch);
            Assert.True(status.IsClean);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetStatus_UntrackedFile_ReportsNotCleanWithUntrackedPath()
    {
        var root = InitRepository();
        try
        {
            File.WriteAllText(Path.Combine(root, "new-file.txt"), "content");

            var status = new LocalGitOperations(root).GetStatus();

            Assert.False(status.IsClean);
            Assert.Contains("new-file.txt", status.UntrackedPaths);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CreateBranch_ThenCommit_Succeeds()
    {
        var root = InitRepository();
        try
        {
            var ops = new LocalGitOperations(root);

            var branchResult = ops.CreateBranch("feature/x");
            File.WriteAllText(Path.Combine(root, "new-file.txt"), "content");
            var commitResult = ops.Commit("add new file");

            Assert.True(branchResult.Success);
            Assert.True(commitResult.Success);
            Assert.True(ops.GetStatus().IsClean);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CheckMergeReadiness_DefaultBranch_IsNotReady()
    {
        var root = InitRepository();
        try
        {
            var result = new LocalGitOperations(root).CheckMergeReadiness("main", "main");

            Assert.False(result.IsReady);
            Assert.NotEmpty(result.Reasons);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CheckMergeReadiness_CleanFeatureBranch_IsReady()
    {
        var root = InitRepository();
        try
        {
            var ops = new LocalGitOperations(root);
            ops.CreateBranch("feature/x");

            var result = ops.CheckMergeReadiness("feature/x", "main");

            Assert.True(result.IsReady);
            Assert.Empty(result.Reasons);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
```

- [ ] RED (compile-fail) → implement all five files → GREEN.
- [ ] Full suite green.
- [ ] Commit: `feat(runtime): implement the Git Operations Integration Adapter (status, diff, branch, commit, merge readiness)`.

---

## Task 3: `RepositoryExecutionContext` + `RepositoryContextFactory`

**Files:** Create `src/Runtime/Execution/RepositoryExecutionContext.cs`, `RepositoryContextFactory.cs`; create `tests/Runtime.Tests/Execution/RepositoryContextFactoryTests.cs`.

**Interfaces:**
- Consumes: `LocalFileSystemRepositoryScanner`, `StackDetector.DetectTechnologies` (both unmodified, existing), `IGitOperations` (Task 2).
- Produces: `RepositoryExecutionContext` — consumed by every Repository Intelligence/Planner Behavior (Tasks 5-11) and by `RepositoryValidationSkillBehavior` (Task 12).

```csharp
// RepositoryExecutionContext.cs
using Runtime.DomainContent.Onboarding;

namespace Runtime.Execution;

// One target repository's assessed state (P3 §1: Repository Context) -
// distinct from both RuntimeExecutionContext (the four-tier per-instance
// concept) and EngineeringExecutionContext (P2's session-level concept).
// TechnologyProfile reuses AGT-EXE-0007's own TechnologyProfileDraft
// verbatim (Architecture Decision 4) - AGT-EXE-0015's own Role #1
// ("consume, never re-derive") is honored structurally: nothing in
// Runtime.DomainContent.RepositoryIntelligence ever calls StackDetector
// itself, only this factory does.
public sealed record RepositoryExecutionContext(
    string RepositoryRoot,
    string RepositoryName,
    bool IsReadable,
    bool IsGitRepository,
    string? CurrentBranch,
    bool? IsWorkingTreeClean,
    TechnologyProfileDraft? TechnologyProfile);
```

```csharp
// RepositoryContextFactory.cs
using Runtime.DomainContent.Onboarding;
using Runtime.Integration.GitOperations;
using Runtime.Integration.RepositoryScanning;

namespace Runtime.Execution;

// Composes the existing S22/S23 scanner+detector with the new Git
// Operations adapter (Task 2) into one RepositoryExecutionContext -
// mirrors RepositoryOnboardingAssessmentAgent's own established
// composition pattern (never re-implements StackDetector's reasoning).
public static class RepositoryContextFactory
{
    public static RepositoryExecutionContext Create(string repositoryName, string repositoryRoot)
    {
        var scanner = new LocalFileSystemRepositoryScanner(repositoryRoot);
        var isReadable = scanner.IsRootReadable();

        TechnologyProfileDraft? technologyProfile = null;
        if (isReadable)
        {
            var detection = StackDetector.DetectTechnologies(scanner);
            technologyProfile = new TechnologyProfileDraft($"TPR-CTX-{Guid.NewGuid():N}"[..16], detection.Technologies);
        }

        var gitOperations = new LocalGitOperations(repositoryRoot);
        var isGitRepository = isReadable && gitOperations.IsGitRepository();

        string? currentBranch = null;
        bool? isWorkingTreeClean = null;
        if (isGitRepository)
        {
            var status = gitOperations.GetStatus();
            currentBranch = status.CurrentBranch;
            isWorkingTreeClean = status.IsClean;
        }

        return new RepositoryExecutionContext(repositoryRoot, repositoryName, isReadable, isGitRepository, currentBranch, isWorkingTreeClean, technologyProfile);
    }
}
```

Test (`tests/Runtime.Tests/Execution/RepositoryContextFactoryTests.cs`):

```csharp
using System.Diagnostics;
using Runtime.Execution;

namespace Runtime.Tests.Execution;

public class RepositoryContextFactoryTests
{
    [Fact]
    public void Create_RealDotnetGitRepository_ProducesFullyPopulatedContext()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            RunGit(root, "init", "-b", "main");
            RunGit(root, "config", "user.email", "test@example.com");
            RunGit(root, "config", "user.name", "Test User");
            File.WriteAllText(Path.Combine(root, "app.csproj"), "<Project />");
            RunGit(root, "add", "-A");
            RunGit(root, "commit", "-m", "initial");

            var context = RepositoryContextFactory.Create("test-repo", root);

            Assert.True(context.IsReadable);
            Assert.True(context.IsGitRepository);
            Assert.Equal("main", context.CurrentBranch);
            Assert.True(context.IsWorkingTreeClean);
            Assert.NotNull(context.TechnologyProfile);
            Assert.Contains(context.TechnologyProfile!.Technologies, t => t.Name == "dotnet");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Create_NonExistentRoot_ReportsUnreadableWithNoTechnologyProfile()
    {
        var context = RepositoryContextFactory.Create("missing-repo", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        Assert.False(context.IsReadable);
        Assert.Null(context.TechnologyProfile);
    }

    private static void RunGit(string root, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in arguments) startInfo.ArgumentList.Add(arg);
        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
    }
}
```

- [ ] RED → implement → GREEN → full suite green.
- [ ] Commit: `feat(runtime): implement RepositoryExecutionContext and RepositoryContextFactory`.

---

## Task 4: Extend the Engineering Architecture Manifest

**Files:** Modify `src/Host/EngineeringArchitectureManifest.cs`; modify `tests/Host.Tests/RuntimeArchitectureLoaderTests.cs` (and its own `KnownArtifacts_...` assertion test, wherever it lives — same file P2's Task 2 touched).

Add to `EngineeringArchitectureManifest.KnownArtifacts`:
```csharp
new ArchitectureArtifact("AGT-EXE-0012", "Execution"),
new ArchitectureArtifact("SKL-EXE-0023", "Execution"),
new ArchitectureArtifact("SKL-EXE-0024", "Execution"),
new ArchitectureArtifact("SKL-EXE-0025", "Execution"),
new ArchitectureArtifact("WFL-DEL-0006", "Delivery"),
new ArchitectureArtifact("AGT-EXE-0015", "Execution"),
new ArchitectureArtifact("SKL-EXE-0030", "Execution"),
new ArchitectureArtifact("SKL-EXE-0031", "Execution"),
new ArchitectureArtifact("SKL-EXE-0032", "Execution"),
new ArchitectureArtifact("SKL-EXE-0033", "Execution"),
new ArchitectureArtifact("WFL-DEL-0008", "Delivery"),
```
(24 + 11 = 35 total.)

Add matching `Assert.Contains` lines for each new Id in the existing `KnownArtifacts_...` test.

- [ ] Add the 11 entries + assertions (RED first).
- [ ] GREEN: `dotnet test tests/Host.Tests/Host.Tests.csproj`.
- [ ] Full suite green.
- [ ] Commit: `feat(host): extend the manifest with Engineering Planner and Repository Intelligence artifacts`.

**Note:** as in P2, registering these Ids now (before their Behaviors exist) is safe — Registry registration doesn't require a bound Behavior, only `SkillHost.Invoke` does, at call time. Behaviors are wired in Task 14's `Program.cs` update.

---

## Task 5: Repository Intelligence types + `SKL-EXE-0030` (Repository & Architecture Understanding)

**Files:** Create `src/Runtime/DomainContent/RepositoryIntelligence/RepositoryIntelligenceTypes.cs`, `RepositoryAndArchitectureUnderstandingBehavior.cs`; create `tests/Runtime.Tests/DomainContent/RepositoryIntelligence/RepositoryAndArchitectureUnderstandingBehaviorTests.cs`.

```csharp
// RepositoryIntelligenceTypes.cs — shared across all four Skills in this
// folder, mirroring PCT-EXE-0030/0031/0032/0033's own Input/Output shapes
// exactly.
using Runtime.Execution;

namespace Runtime.DomainContent.RepositoryIntelligence;

public enum FindingClass { Pass, Warning, Fail, Unknown }

// Findings-Manifest-by-stable-Id shape (SCH-DEL-0005 §"Required sections").
public sealed record IntelligenceFinding(string Id, string Area, FindingClass Class, string Summary, string Evidence, bool SpotChecked = false);

// PCT-EXE-0030
public sealed record RepositoryUnderstandingRequest(string RequestScope, RepositoryExecutionContext RepositoryContext);
public sealed record RepositoryUnderstandingResult(IReadOnlyList<IntelligenceFinding> Findings, string Answer);

// PCT-EXE-0031
public sealed record CodeIntelligenceRequest(string RequestScope, RepositoryExecutionContext RepositoryContext);
public sealed record CodeIntelligenceResult(IReadOnlyList<IntelligenceFinding> Findings);

// PCT-EXE-0032
public sealed record EvidenceChainStep(string Step, IReadOnlyList<string> EvdIds, string InferentialLink);
public sealed record AlternativeHypothesis(string Hypothesis, string SupportingEvidence);
public sealed record DiagnosticsRequest(string ReportedProblem, IReadOnlyList<string> EvidenceSources);
public sealed record DiagnosticsResult(
    IReadOnlyList<EvidenceChainStep> EvidenceChain,
    int ConfidenceScore,
    string ConfidenceJustification,
    IReadOnlyList<AlternativeHypothesis> AlternativeHypotheses,
    IReadOnlyList<string> ConflictingEvidence)
{
    public static DiagnosticsResult NoEvidenceSupplied() => new(
        Array.Empty<EvidenceChainStep>(), 0,
        "No evidence sources were supplied - a root cause cannot be assessed without evidence, per PCT-EXE-0032's own precondition.",
        Array.Empty<AlternativeHypothesis>(), Array.Empty<string>());
}

// PCT-EXE-0033
public sealed record CombinedIntelligenceFindings(
    RepositoryUnderstandingResult? Understanding,
    CodeIntelligenceResult? CodeIntelligence,
    DiagnosticsResult? Diagnostics);
public sealed record PendingReviewInstance(string ReviewTarget, string Summary);
public sealed record RecommendationReport(
    string ReportType,
    string AssessedRepositoryScope,
    IReadOnlyList<IntelligenceFinding> Findings,
    IReadOnlyList<string> ProvenanceNotes);
public sealed record RecommendationSynthesisResult(RecommendationReport ReportInstance, bool RequiresHumanApproval, PendingReviewInstance? ReviewInstance);
```

```csharp
// RepositoryAndArchitectureUnderstandingBehavior.cs
using Runtime.Context;
using Runtime.Hosts.Skill;
using Runtime.Integration.RepositoryScanning;

namespace Runtime.DomainContent.RepositoryIntelligence;

// The executable realization of SKL-EXE-0030 (framework/execution/skills/
// repository-and-architecture-understanding.md)'s Behavior. The governed
// Skill spec is free-text/NL interpretation with no LLM integration point
// anywhere in this Runtime (same rationale as SKL-EXE-0021) - this is
// therefore a deterministic, evidence-only structural search: it answers
// whether a name mentioned in request_scope exists as a real top-level (or
// one-level-nested) directory/file entry, citing the real path as
// evidence, and reports UNKNOWN with an honest rationale otherwise -
// never a fabricated claim (PCT-EXE-0030's own postcondition).
public sealed class RepositoryAndArchitectureUnderstandingBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("RepositoryUnderstandingRequest", out var raw) || raw is not RepositoryUnderstandingRequest request)
        {
            return new RepositoryUnderstandingResult(
                new[] { new IntelligenceFinding("RAU-0001", "Repository Understanding", FindingClass.Unknown, "No request was supplied.", "n/a") },
                "No request was supplied to answer.");
        }

        if (!request.RepositoryContext.IsReadable)
        {
            var finding = new IntelligenceFinding("RAU-0001", "Repository Understanding", FindingClass.Unknown,
                $"Repository '{request.RepositoryContext.RepositoryName}' is not readable.", request.RepositoryContext.RepositoryRoot);
            return new RepositoryUnderstandingResult(new[] { finding }, "The repository could not be read.");
        }

        var scanner = new LocalFileSystemRepositoryScanner(request.RepositoryContext.RepositoryRoot);
        var match = FindMatch(scanner, request.RequestScope);

        if (match is null)
        {
            var finding = new IntelligenceFinding("RAU-0001", "Repository Understanding", FindingClass.Unknown,
                $"No top-level or one-level-nested entry matching '{request.RequestScope}' was found.",
                request.RepositoryContext.RepositoryRoot);
            return new RepositoryUnderstandingResult(new[] { finding }, $"Could not locate '{request.RequestScope}' within the scanned bound.");
        }

        var passFinding = new IntelligenceFinding("RAU-0001", "Repository Understanding", FindingClass.Pass,
            $"Found an entry matching '{request.RequestScope}'.", match);
        return new RepositoryUnderstandingResult(new[] { passFinding }, $"'{request.RequestScope}' exists at '{match}'.");
    }

    private static string? FindMatch(IRepositoryScanner scanner, string requestScope)
    {
        var rootListing = scanner.ListEntries(string.Empty);
        if (rootListing.Status != RepositoryScanStatus.Success)
        {
            return null;
        }

        foreach (var entry in rootListing.Entries)
        {
            if (entry.Name.Contains(requestScope, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Name;
            }

            if (entry.IsDirectory)
            {
                var nested = scanner.ListEntries(entry.Name);
                if (nested.Status == RepositoryScanStatus.Success && nested.Entries.Any(e => e.Name.Contains(requestScope, StringComparison.OrdinalIgnoreCase)))
                {
                    return entry.Name + " (contains a matching entry)";
                }
            }
        }

        return null;
    }
}
```

Tests (`RepositoryAndArchitectureUnderstandingBehaviorTests.cs`) — three cases: no request supplied, real temp repo with a matching directory (`Pass`), real temp repo with no match (`Unknown`). Follow Task 4's own `RuntimeExecutionContext` construction pattern (`Tier3SkillContent["RepositoryUnderstandingRequest"] = ...`).

- [ ] RED → implement → GREEN → full suite green.
- [ ] Commit: `feat(runtime): implement SKL-EXE-0030 Repository and Architecture Understanding`.

---

## Task 6: `SKL-EXE-0031` (Code Intelligence & Quality Signal Detection)

**Files:** Create `src/Runtime/DomainContent/RepositoryIntelligence/CodeIntelligenceAndQualitySignalDetectionBehavior.cs`; create matching test file.

```csharp
using Runtime.Context;
using Runtime.Hosts.Skill;
using Runtime.Integration.RepositoryScanning;

namespace Runtime.DomainContent.RepositoryIntelligence;

// The executable realization of SKL-EXE-0031. AGT-EXE-0015's own Role #3
// explicitly sanctions "UNKNOWN-with-rationale otherwise" for any signal
// this Runtime has no Technology-Profile-resolved Automation for - so
// duplication/complexity/dead-code/anti-pattern detection (which require
// real static-analysis tooling this Runtime does not have) are reported
// UNKNOWN, never fabricated. The one signal genuinely spot-checkable with
// existing tools: whether a detected stack has an accompanying
// test-project/test-directory convention present (evidenced via the
// existing scanner - spot_checked: true because the absence/presence is
// independently verified against the real listing, per PCT-EXE-0031's own
// postcondition).
public sealed class CodeIntelligenceAndQualitySignalDetectionBehavior : IBehavior
{
    private static readonly string[] TestDirectoryMarkers = { "tests", "test", "spec", "specs" };

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("CodeIntelligenceRequest", out var raw) || raw is not CodeIntelligenceRequest request)
        {
            return new CodeIntelligenceResult(new[] { Unknown("CIQ-0001", "No request was supplied.") });
        }

        var findings = new List<IntelligenceFinding>();

        if (request.RepositoryContext.TechnologyProfile is null || request.RepositoryContext.TechnologyProfile.Technologies.Count == 0)
        {
            findings.Add(Unknown("CIQ-0001", "No Technology Profile is available - test-convention detection requires a detected stack."));
        }
        else
        {
            var scanner = new LocalFileSystemRepositoryScanner(request.RepositoryContext.RepositoryRoot);
            var rootListing = scanner.ListEntries(string.Empty);
            var hasTestDirectory = rootListing.Status == RepositoryScanStatus.Success
                && rootListing.Entries.Any(e => e.IsDirectory && TestDirectoryMarkers.Contains(e.Name, StringComparer.OrdinalIgnoreCase));

            findings.Add(hasTestDirectory
                ? new IntelligenceFinding("CIQ-0001", "Test Convention", FindingClass.Pass, "A conventional test directory was found at the repository root.", request.RepositoryContext.RepositoryRoot, SpotChecked: true)
                : new IntelligenceFinding("CIQ-0001", "Test Convention", FindingClass.Warning, "No conventional test directory (tests/test/spec/specs) was found at the repository root.", request.RepositoryContext.RepositoryRoot, SpotChecked: true));
        }

        findings.Add(Unknown("CIQ-0002", "No Technology-Profile-resolved Automation exists in this Runtime for technical debt, dead code, duplication, complexity, or anti-pattern detection - reported UNKNOWN rather than fabricated, per AGT-EXE-0015's own Role #3."));

        return new CodeIntelligenceResult(findings);
    }

    private static IntelligenceFinding Unknown(string id, string rationale) =>
        new(id, "Code Intelligence", FindingClass.Unknown, rationale, "n/a");
}
```

Tests: no-request case; repository with a `tests/` directory (`Pass`); repository without one (`Warning`); always includes the `CIQ-0002` `Unknown` finding.

- [ ] RED → implement → GREEN → full suite green.
- [ ] Commit: `feat(runtime): implement SKL-EXE-0031 Code Intelligence and Quality Signal Detection`.

---

## Task 7: `SKL-EXE-0032` (Engineering Diagnostics & Root Cause Analysis)

**Files:** Create `src/Runtime/DomainContent/RepositoryIntelligence/EngineeringDiagnosticsAndRootCauseAnalysisBehavior.cs`; create matching test file.

```csharp
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.RepositoryIntelligence;

// The executable realization of SKL-EXE-0032. Real causal inference over
// logs/telemetry/exceptions is free-text/NL reasoning with no
// deterministic-algorithm precedent and no LLM integration point in this
// Runtime - so this Behavior performs the mechanical parts PCT-EXE-0032
// actually specifies (one Evidence Chain step per supplied evidence
// source, non-empty Alternative Hypotheses whenever confidence is not
// high) and fixes confidence low with an honest justification, rather
// than fabricating a causal narrative it cannot support. This is the
// correct, non-hollow realization of a contract whose own postconditions
// already anticipate and require this honesty.
public sealed class EngineeringDiagnosticsAndRootCauseAnalysisBehavior : IBehavior
{
    private const int DeterministicConfidenceScore = 2;

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("DiagnosticsRequest", out var raw) || raw is not DiagnosticsRequest request)
        {
            return DiagnosticsResult.NoEvidenceSupplied();
        }

        if (request.EvidenceSources.Count == 0)
        {
            return DiagnosticsResult.NoEvidenceSupplied();
        }

        var evidenceChain = request.EvidenceSources
            .Select((source, index) => new EvidenceChainStep($"Step {index + 1}: consider evidence source", new[] { source }, "Enumerated as a candidate contributor - no automated causal inference is available to establish a stronger link."))
            .ToList();

        var alternativeHypotheses = request.EvidenceSources
            .Select(source => new AlternativeHypothesis($"'{source}' is the primary contributor.", source))
            .ToList();

        return new DiagnosticsResult(
            evidenceChain,
            DeterministicConfidenceScore,
            "This Runtime has no LLM-backed or deterministic-algorithm causal-inference capability - confidence is fixed low and every evidence source is surfaced as an equally-weighted alternative hypothesis rather than a fabricated conclusion.",
            alternativeHypotheses,
            Array.Empty<string>());
    }
}
```

Tests: no request supplied → `NoEvidenceSupplied`; empty `EvidenceSources` → `NoEvidenceSupplied`; two evidence sources → `EvidenceChain.Count == 2`, `AlternativeHypotheses.Count == 2`, `ConfidenceScore == 2`.

- [ ] RED → implement → GREEN → full suite green.
- [ ] Commit: `feat(runtime): implement SKL-EXE-0032 Engineering Diagnostics and Root Cause Analysis`.

---

## Task 8: `SKL-EXE-0033` (Recommendation Synthesis & Evidence-Based Reporting)

**Files:** Create `src/Runtime/DomainContent/RepositoryIntelligence/RecommendationSynthesisAndEvidenceBasedReportingBehavior.cs`; create matching test file.

```csharp
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.RepositoryIntelligence;

// The executable realization of SKL-EXE-0033. requires_human_approval is
// set per PCT-EXE-0033's own postcondition: true whenever any finding is
// a Root Cause Conclusion (Diagnostics ran), an Architecture
// Recommendation, or a Risk Classification (approximated here as any FAIL
// finding, the only class this Runtime's deterministic Behaviors can
// produce that plausibly warrants one) - never silently bypassed.
public sealed class RecommendationSynthesisAndEvidenceBasedReportingBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("CombinedIntelligenceFindings", out var raw) || raw is not CombinedIntelligenceFindings combined)
        {
            var report = new RecommendationReport("Understanding", "unknown", Array.Empty<IntelligenceFinding>(), new[] { "No combined findings were supplied." });
            return new RecommendationSynthesisResult(report, false, null);
        }

        var findings = new List<IntelligenceFinding>();
        findings.AddRange(combined.Understanding?.Findings ?? Array.Empty<IntelligenceFinding>());
        findings.AddRange(combined.CodeIntelligence?.Findings ?? Array.Empty<IntelligenceFinding>());

        var diagnosticsRan = combined.Diagnostics is not null;
        var hasFailFinding = findings.Any(f => f.Class == FindingClass.Fail);
        var requiresApproval = diagnosticsRan || hasFailFinding;

        var reportType = combined.Diagnostics is not null ? "Root Cause Analysis" : "Understanding";
        var provenanceNotes = new List<string>
        {
            $"Analysis Date: {DateTime.UtcNow:O}",
            "Analysis Tooling: aef-platform deterministic Repository Intelligence Behaviors (no LLM integration).",
        };

        var report = new RecommendationReport(reportType, "request-scoped (see Findings evidence)", findings, provenanceNotes);

        PendingReviewInstance? reviewInstance = requiresApproval
            ? new PendingReviewInstance(diagnosticsRan ? "Root Cause Conclusion" : "Risk Classification", $"{findings.Count} finding(s) require Human Approval before this report is treated as final.")
            : null;

        return new RecommendationSynthesisResult(report, requiresApproval, reviewInstance);
    }
}
```

Tests: no combined findings supplied; understanding-only (no diagnostics, no FAIL) → `RequiresHumanApproval == false`, `ReviewInstance == null`; with `Diagnostics` present → `RequiresHumanApproval == true` and `ReviewInstance != null`.

- [ ] RED → implement → GREEN → full suite green.
- [ ] Commit: `feat(runtime): implement SKL-EXE-0033 Recommendation Synthesis and Evidence-Based Reporting`.

---

## Task 9: Engineering Planner types + `SKL-EXE-0023` (Engineering Discovery and Goal Clarification)

**Files:** Create `src/Runtime/DomainContent/EngineeringPlanning/EngineeringPlanningTypes.cs`, `EngineeringDiscoveryAndGoalClarificationBehavior.cs`; create matching test file.

```csharp
// EngineeringPlanningTypes.cs — mirrors PCT-EXE-0023/0024/0025 exactly.
using Runtime.Execution;

namespace Runtime.DomainContent.EngineeringPlanning;

// PCT-EXE-0023
public sealed record DiscoveryFindings(IReadOnlyList<string> ExistingCapability, IReadOnlyList<string> Gaps);
public sealed record DiscoveryRequest(string Goal, RepositoryExecutionContext RepositoryContext);
public sealed record DiscoveryResult(string Status, DiscoveryFindings? Findings, IReadOnlyList<string>? ClarificationQuestions)
{
    public static DiscoveryResult Complete(DiscoveryFindings findings) => new("discovery_complete", findings, null);
    public static DiscoveryResult NeedsClarification(IReadOnlyList<string> questions) => new("needs_clarification", null, questions);
}

// PCT-EXE-0024 — field names mirror SCH-DEL-0003's own "Always-required
// sections" table exactly, so a conforming instance can be assembled
// mechanically. OpenItems names every section this Behavior could not
// resolve without real judgment - never silently blank (PCT-EXE-0024's
// own postcondition).
public sealed record SpecificationDraft(
    string SpecificationType,
    string GoalProblemStatement,
    string SuccessCriteria,
    string ExistingCapabilityAnalysis,
    string FunctionalRequirements,
    string OutOfScope,
    string Risks,
    IReadOnlyList<string> Assumptions,
    string AcceptanceCriteria);
public sealed record DraftingRequest(string Goal, DiscoveryFindings DiscoveryFindings);
public sealed record DraftingResult(SpecificationDraft SpecificationDraft, IReadOnlyList<string> AlignmentFindings, IReadOnlyList<string> OpenItems);

// PCT-EXE-0025
public sealed record ReadinessRequest(SpecificationDraft SpecificationDraft, IReadOnlyList<string> AlignmentFindings, IReadOnlyList<string> OpenItems);
public sealed record ReadinessResult(string ImplementationReadiness, string ApprovalSummary, string Status);
```

```csharp
// EngineeringDiscoveryAndGoalClarificationBehavior.cs
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.EngineeringPlanning;

// The executable realization of SKL-EXE-0023. "Existing capability"
// discovery is a real, mechanical fact drawn from the RepositoryContext's
// own Technology Profile (never fabricated); "gaps" are a small, fixed,
// documented checklist of conventional repository structure (test
// directory, README) - the same bounded, evidence-only style
// StackDetector's own marker-table already establishes. Goal-scoped
// codebase search beyond this (the Skill's own full Role) is free-text/NL
// judgment with no deterministic precedent - out of scope, same rationale
// as SKL-EXE-0021/0030.
public sealed class EngineeringDiscoveryAndGoalClarificationBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("DiscoveryRequest", out var raw) || raw is not DiscoveryRequest request)
        {
            return DiscoveryResult.NeedsClarification(new[] { "No request was supplied to discover against." });
        }

        if (string.IsNullOrWhiteSpace(request.Goal))
        {
            return DiscoveryResult.NeedsClarification(new[] { "What is the goal or business objective for this Specification?" });
        }

        var existingCapability = request.RepositoryContext.TechnologyProfile?.Technologies.Select(t => $"{t.Name} ({t.Basis})").ToList()
            ?? new List<string>();

        var gaps = new List<string>();
        if (request.RepositoryContext.TechnologyProfile is null || request.RepositoryContext.TechnologyProfile.Technologies.Count == 0)
        {
            gaps.Add("No detectable technology stack was found - the repository's build/test tooling could not be identified.");
        }

        return DiscoveryResult.Complete(new DiscoveryFindings(existingCapability, gaps));
    }
}
```

Tests: no request → needs clarification; empty goal → needs clarification with the goal question; real request with a populated `TechnologyProfile` → `discovery_complete` with `ExistingCapability` containing the detected technology name.

- [ ] RED → implement → GREEN → full suite green.
- [ ] Commit: `feat(runtime): implement SKL-EXE-0023 Engineering Discovery and Goal Clarification`.

---

## Task 10: `SKL-EXE-0024` (Specification Drafting and Alignment)

**Files:** Create `src/Runtime/DomainContent/EngineeringPlanning/SpecificationDraftingAndAlignmentBehavior.cs`; create matching test file.

```csharp
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.EngineeringPlanning;

// The executable realization of SKL-EXE-0024. Authoring real prose for
// Success Criteria/Functional Requirements/Risks/Acceptance Criteria is
// free-text/NL judgment with no deterministic precedent - every
// Always-required SCH-DEL-0003 section is still populated (conformance
// requires this), but sections this Behavior cannot honestly derive state
// so explicitly and are also recorded in open_items - never silently
// blank, never invented (PCT-EXE-0024's own postcondition).
public sealed class SpecificationDraftingAndAlignmentBehavior : IBehavior
{
    private const string RequiresHumanInput = "Not determined by automated discovery - requires human input. See Open Items.";

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("DraftingRequest", out var raw) || raw is not DraftingRequest request)
        {
            var emptyDraft = new SpecificationDraft("Standard", RequiresHumanInput, RequiresHumanInput, RequiresHumanInput, RequiresHumanInput, RequiresHumanInput, RequiresHumanInput, Array.Empty<string>(), RequiresHumanInput);
            return new DraftingResult(emptyDraft, Array.Empty<string>(), new[] { "No drafting request was supplied." });
        }

        var openItems = new List<string>();
        var existingCapabilityAnalysis = request.DiscoveryFindings.ExistingCapability.Count > 0
            ? $"Existing capability, per Repository Intelligence: {string.Join("; ", request.DiscoveryFindings.ExistingCapability)}."
            : "No existing capability was detected by automated discovery.";

        foreach (var gap in request.DiscoveryFindings.Gaps)
        {
            openItems.Add(gap);
        }

        openItems.Add("Success Criteria requires human input - automated discovery cannot derive measurable success criteria from a goal statement alone.");
        openItems.Add("Functional Requirements requires human input - automated discovery cannot derive user-facing behavior from a goal statement alone.");
        openItems.Add("Acceptance Criteria requires human input - automated discovery cannot derive completion criteria from a goal statement alone.");

        var draft = new SpecificationDraft(
            SpecificationType: "Standard",
            GoalProblemStatement: request.Goal,
            SuccessCriteria: RequiresHumanInput,
            ExistingCapabilityAnalysis: existingCapabilityAnalysis,
            FunctionalRequirements: RequiresHumanInput,
            OutOfScope: "Not determined by automated discovery.",
            Risks: "Not assessed by automated discovery.",
            Assumptions: Array.Empty<string>(),
            AcceptanceCriteria: RequiresHumanInput);

        return new DraftingResult(draft, Array.Empty<string>(), openItems);
    }
}
```

Tests: no request → empty draft with a single open item; real request with discovery findings containing one capability and one gap → `ExistingCapabilityAnalysis` mentions the capability, `OpenItems` contains the gap plus the three fixed human-input items.

- [ ] RED → implement → GREEN → full suite green.
- [ ] Commit: `feat(runtime): implement SKL-EXE-0024 Specification Drafting and Alignment`.

---

## Task 11: `SKL-EXE-0025` (Readiness Classification and Approval Request)

**Files:** Create `src/Runtime/DomainContent/EngineeringPlanning/ReadinessClassificationAndApprovalRequestBehavior.cs`; create matching test file.

```csharp
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.EngineeringPlanning;

// The executable realization of SKL-EXE-0025. status is always
// "pending_approval" per PCT-EXE-0025's own postcondition - this contract
// never self-renders the Business Approval decision. Readiness is
// classified from OpenItems/AlignmentFindings counts alone - never a
// default guess.
public sealed class ReadinessClassificationAndApprovalRequestBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("ReadinessRequest", out var raw) || raw is not ReadinessRequest request)
        {
            return new ReadinessResult("Requires Clarification", "No readiness request was supplied.", "pending_approval");
        }

        var readiness = (request.OpenItems.Count, request.AlignmentFindings.Count) switch
        {
            (0, 0) => "Ready for Implementation",
            (> 0, _) => "Requires Clarification",
            (0, > 0) => "Requires Redesign",
        };

        var summary = $"{request.OpenItems.Count} open item(s), {request.AlignmentFindings.Count} alignment finding(s). Classified '{readiness}'.";
        return new ReadinessResult(readiness, summary, "pending_approval");
    }
}
```

Tests: no request → `Requires Clarification`; zero open items and zero alignment findings → `Ready for Implementation`; non-zero open items → `Requires Clarification`; zero open items with alignment findings → `Requires Redesign`. `Status` is always `"pending_approval"` in every case.

- [ ] RED → implement → GREEN → full suite green.
- [ ] Commit: `feat(runtime): implement SKL-EXE-0025 Readiness Classification and Approval Request`.

---

## Task 12: Repository Validation — `BuildSystemDetector` + `RepositoryValidationSkillBehavior`

**Files:** Create `src/Runtime/DomainContent/RepositoryValidation/BuildSystemDetector.cs`, `RepositoryValidationSkillBehavior.cs`; create `tests/Runtime.Tests/DomainContent/RepositoryValidation/BuildSystemDetectorTests.cs`, `RepositoryValidationSkillBehaviorTests.cs`.

**Interfaces:** Reuses `Runtime.DomainContent.DeliveryExecution.{ValidationRequest, ValidationOutcome, DeliveryValidationBehavior}` and `Runtime.Integration.DeliveryExecution.IVerificationRunner` verbatim (Gap Analysis: Reuse).

```csharp
// BuildSystemDetector.cs
namespace Runtime.DomainContent.RepositoryValidation;

// Maps a StackDetector-named stack to its conventional default
// build/test command (P3 §4: Build system detection). A stack absent
// from this table has no known default command - callers must treat that
// as "no verification procedure could be constructed", never guess one,
// exactly matching DeliveryValidationBehavior's own escalation
// philosophy for a null Command.
public static class BuildSystemDetector
{
    private static readonly IReadOnlyDictionary<string, (string Command, string[] Arguments)> DefaultCommands =
        new Dictionary<string, (string, string[])>(StringComparer.OrdinalIgnoreCase)
        {
            ["dotnet"] = ("dotnet", new[] { "test" }),
            ["node"] = ("npm", new[] { "test" }),
            ["python"] = ("python", new[] { "-m", "pytest" }),
            ["java"] = ("mvn", new[] { "test" }),
            ["rust"] = ("cargo", new[] { "test" }),
            ["go"] = ("go", new[] { "test", "./..." }),
        };

    public static (string Command, IReadOnlyList<string> Arguments)? DetectDefaultValidationCommand(IReadOnlyList<string> detectedStackNames)
    {
        foreach (var stack in detectedStackNames)
        {
            if (DefaultCommands.TryGetValue(stack, out var command))
            {
                return (command.Command, command.Arguments);
            }
        }

        return null;
    }
}
```

```csharp
// RepositoryValidationSkillBehavior.cs
using Runtime.Context;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.Execution;
using Runtime.Hosts.Skill;
using Runtime.Integration.DeliveryExecution;

namespace Runtime.DomainContent.RepositoryValidation;

// P3 §6 (Validation Pipeline): derives the Command/Arguments from the
// RepositoryExecutionContext's own detected Technology Profile via
// BuildSystemDetector, then delegates to the existing, unmodified
// DeliveryValidationBehavior/IVerificationRunner - the exact same reuse
// pattern the Delivery Execution SkillHost adapters (P2 Task 6) already
// established for per-invocation request binding.
public sealed class RepositoryValidationSkillBehavior : IBehavior
{
    private readonly IVerificationRunner _runner;

    public RepositoryValidationSkillBehavior(IVerificationRunner? runner = null)
    {
        _runner = runner ?? new ProcessVerificationRunner();
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("RepositoryContext", out var raw) || raw is not RepositoryExecutionContext repositoryContext)
        {
            return ValidationOutcome.Escalated("No RepositoryExecutionContext was supplied - cannot construct a verification procedure.");
        }

        var stackNames = repositoryContext.TechnologyProfile?.Technologies.Select(t => t.Name).ToList() ?? new List<string>();
        var command = BuildSystemDetector.DetectDefaultValidationCommand(stackNames);

        if (command is null)
        {
            return ValidationOutcome.Escalated($"No known default build/test command for repository '{repositoryContext.RepositoryName}''s detected stack - halting rather than guessing one.");
        }

        var request = new ValidationRequest(repositoryContext.RepositoryName, repositoryContext.RepositoryRoot, command.Value.Command, command.Value.Arguments);
        return new DeliveryValidationBehavior(request, _runner).Execute(context);
    }
}
```

Tests (`BuildSystemDetectorTests.cs`): `["dotnet"]` → `dotnet test`; `["node"]` → `npm test`; `["unknown-stack"]` → `null`; empty list → `null`.

Tests (`RepositoryValidationSkillBehaviorTests.cs`, using a fake `IVerificationRunner` returning a fixed `VerificationRunResult`, mirroring `DeliveryValidationBehaviorTests`' own fake-runner pattern): no `RepositoryContext` supplied → escalates; a context with an unrecognized/empty Technology Profile → escalates; a context with `TechnologyProfile.Technologies` containing `"dotnet"` → delegates and returns a `Recorded` outcome with `StoryReferenceId` equal to the repository name.

- [ ] RED → implement all four files → GREEN → full suite green.
- [ ] Commit: `feat(runtime): implement Repository Validation (BuildSystemDetector, RepositoryValidationSkillBehavior)`.

---

## Task 13: Extend `ExecutionResult` and wire all new dispatch paths into `EngineeringExecutionPipeline`

**Files:** Modify `src/Runtime/Execution/ExecutionResult.cs`; modify `src/Runtime/Execution/EngineeringExecutionPipeline.cs`; modify `src/Runtime/DomainContent/ConversationManagement/ConversationIntakeAndRoutingBehavior.cs`; modify `tests/Runtime.Tests/Execution/EngineeringExecutionPipelineTests.cs` (existing `ExecutionResult` constructions gain two trailing `null`s; new tests added); modify `tests/Runtime.Tests/DomainContent/ConversationManagement/ConversationIntakeAndRoutingBehaviorTests.cs` (new keyword cases).

```csharp
// ExecutionResult.cs — three new, additive, nullable fields (Architecture Decision 5).
namespace Runtime.Execution;

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
    bool? TestsExecuted = null);
```

Using a trailing-optional-parameter default keeps every existing positional construction site (`FromOrchestration`, the P2 `FromDeliveryExecution`, and every existing test) compiling unchanged — no mechanical edit is actually required for those call sites, reducing Architecture Decision 5's "mechanical, non-behavioral edit" to zero touched lines beyond this file. Confirm this by building immediately after this one-file change, before touching the pipeline.

- [ ] Make the `ExecutionResult` change alone. `dotnet build AefPlatform.sln` succeeds (proves every existing call site still compiles via the new fields' defaults).
- [ ] Commit: `feat(runtime): extend ExecutionResult with RepositoryAnalyzed, FilesChanged, TestsExecuted`.

Now extend `ConversationIntakeAndRoutingBehavior`'s `KnownCapabilities` table (append, do not reorder or remove existing entries):

```csharp
("analyze", "WFL-DEL-0008"),
("understand", "WFL-DEL-0008"),
("diagnose", "WFL-DEL-0008"),
("plan", "WFL-DEL-0006"),
("specify", "WFL-DEL-0006"),
("validate", "SKL-EXE-0031-VALIDATION"),
```

Wait — a routing keyword must name a real `TargetCapabilityId` the pipeline's `Dispatch` switch actually handles (never a capability with no executable realization, per this Behavior's own doc comment). Use `"REPO-VALIDATION"` as the capability id for the standalone validation path (an implementation-only sentinel Id, not a governed artifact Id, exactly mirroring how the Prompt asks Runtime Integration to dispatch, not asking for a new governed Workflow Id):

```csharp
("analyze", "WFL-DEL-0008"),
("understand", "WFL-DEL-0008"),
("diagnose", "WFL-DEL-0008"),
("plan", "WFL-DEL-0006"),
("specify", "WFL-DEL-0006"),
("validate", "REPO-VALIDATION"),
```

Update this Behavior's own doc comment to add: "..., `WFL-DEL-0006`, `WFL-DEL-0008`, and the repository validation path — it never routes to a capability with no executable realization." Update its clarification message: `"Which capability should handle this request? Supported: feature, epic, delivery, analyze, plan, validate."`.

Extend `EngineeringExecutionPipeline`:

```csharp
// EngineeringExecutionPipeline.cs — add these usings:
using Runtime.DomainContent.EngineeringPlanning;
using Runtime.DomainContent.RepositoryIntelligence;

// Dispatch's switch expression gains three new arms (existing arms unchanged):
private ExecutionResult Dispatch(RoutingResult routingResult, EngineeringRequest request)
{
    return routingResult.TargetCapabilityId switch
    {
        "WFL-DEL-0001" => FromOrchestration(_featureOrchestrator.Start(new ManualTrigger("WFL-DEL-0001", request.Payload)), "WFL-DEL-0001"),
        "WFL-DEL-0004" => FromOrchestration(_epicOrchestrator.Start(new ManualTrigger("WFL-DEL-0004", request.Payload)), "WFL-DEL-0004"),
        "SKL-EXE-0018" => FromDeliveryExecution(request),
        "WFL-DEL-0006" => FromEngineeringPlanning(request),
        "WFL-DEL-0008" => FromRepositoryIntelligence(request),
        "REPO-VALIDATION" => FromRepositoryValidation(request),
        _ => new ExecutionResult(false, null, null, new[] { $"No executable dispatch target for capability '{routingResult.TargetCapabilityId}'." }, null, TimeSpan.Zero, null, null),
    };
}

private static RepositoryExecutionContext RequireRepositoryContext(EngineeringRequest request)
{
    if (request.Payload is not null
        && request.Payload.TryGetValue("RepositoryName", out var nameObj) && nameObj is string name
        && request.Payload.TryGetValue("RepositoryRoot", out var rootObj) && rootObj is string root)
    {
        return RepositoryContextFactory.Create(name, root);
    }

    return new RepositoryExecutionContext(string.Empty, string.Empty, false, false, null, null, null);
}

private ExecutionResult FromEngineeringPlanning(EngineeringRequest request)
{
    var repositoryContext = RequireRepositoryContext(request);
    var goal = request.Payload is not null && request.Payload.TryGetValue("Goal", out var goalObj) && goalObj is string goalText
        ? goalText
        : request.UserRequest;

    var discovery = InvokeSkill<DiscoveryResult>("SKL-EXE-0023", "DiscoveryRequest", new DiscoveryRequest(goal, repositoryContext), request.ProjectProfileId, "Planning-Discovery");
    if (discovery is null || discovery.Status == "needs_clarification")
    {
        var diagnostics = discovery?.ClarificationQuestions ?? new[] { "SKL-EXE-0023 is not invocable." };
        return new ExecutionResult(false, "WFL-DEL-0006", "AGT-EXE-0012", diagnostics, null, TimeSpan.Zero, null, null);
    }

    var drafting = InvokeSkill<DraftingResult>("SKL-EXE-0024", "DraftingRequest", new DraftingRequest(goal, discovery.Findings!), request.ProjectProfileId, "Planning-Drafting");
    if (drafting is null)
    {
        return new ExecutionResult(false, "WFL-DEL-0006", "AGT-EXE-0012", new[] { "SKL-EXE-0024 is not invocable." }, null, TimeSpan.Zero, null, null);
    }

    var readiness = InvokeSkill<ReadinessResult>("SKL-EXE-0025", "ReadinessRequest", new ReadinessRequest(drafting.SpecificationDraft, drafting.AlignmentFindings, drafting.OpenItems), request.ProjectProfileId, "Planning-Readiness");
    if (readiness is null)
    {
        return new ExecutionResult(false, "WFL-DEL-0006", "AGT-EXE-0012", new[] { "SKL-EXE-0025 is not invocable." }, null, TimeSpan.Zero, null, null);
    }

    var diagnosticsList = drafting.OpenItems.Concat(drafting.AlignmentFindings).ToList();
    return new ExecutionResult(true, "WFL-DEL-0006", "AGT-EXE-0012", diagnosticsList, new[] { readiness.ImplementationReadiness }, TimeSpan.Zero, readiness.ApprovalSummary, "Awaiting Business Approval.",
        RepositoryAnalyzed: repositoryContext.RepositoryName);
}

private ExecutionResult FromRepositoryIntelligence(EngineeringRequest request)
{
    var repositoryContext = RequireRepositoryContext(request);
    var requestScope = request.Payload is not null && request.Payload.TryGetValue("RequestScope", out var scopeObj) && scopeObj is string scopeText
        ? scopeText
        : request.UserRequest;

    var understanding = InvokeSkill<RepositoryUnderstandingResult>("SKL-EXE-0030", "RepositoryUnderstandingRequest", new RepositoryUnderstandingRequest(requestScope, repositoryContext), request.ProjectProfileId, "RepoIntel-Understanding");
    var codeIntelligence = InvokeSkill<CodeIntelligenceResult>("SKL-EXE-0031", "CodeIntelligenceRequest", new CodeIntelligenceRequest(requestScope, repositoryContext), request.ProjectProfileId, "RepoIntel-CodeIntelligence");

    DiagnosticsResult? diagnostics = null;
    if (request.Payload is not null && request.Payload.TryGetValue("ReportedProblem", out var problemObj) && problemObj is string problem)
    {
        var evidenceSources = request.Payload.TryGetValue("EvidenceSources", out var evidenceObj) && evidenceObj is IReadOnlyList<string> sources
            ? sources
            : Array.Empty<string>();
        diagnostics = InvokeSkill<DiagnosticsResult>("SKL-EXE-0032", "DiagnosticsRequest", new DiagnosticsRequest(problem, evidenceSources), request.ProjectProfileId, "RepoIntel-Diagnostics");
    }

    var combined = new CombinedIntelligenceFindings(understanding, codeIntelligence, diagnostics);
    var synthesis = InvokeSkill<RecommendationSynthesisResult>("SKL-EXE-0033", "CombinedIntelligenceFindings", combined, request.ProjectProfileId, "RepoIntel-Synthesis");

    if (synthesis is null)
    {
        return new ExecutionResult(false, "WFL-DEL-0008", "AGT-EXE-0015", new[] { "SKL-EXE-0033 is not invocable." }, null, TimeSpan.Zero, null, null);
    }

    var diagnosticsSummary = synthesis.ReportInstance.Findings.Select(f => $"[{f.Class}] {f.Area}: {f.Summary}").ToList();
    var nextStep = synthesis.RequiresHumanApproval ? "Awaiting Human Approval on Root Cause/Architecture/Risk findings." : "No further action needed.";

    return new ExecutionResult(true, "WFL-DEL-0008", "AGT-EXE-0015", diagnosticsSummary, new[] { understanding?.Answer ?? "n/a" }, TimeSpan.Zero, synthesis.ReportInstance.ReportType, nextStep,
        RepositoryAnalyzed: repositoryContext.RepositoryName);
}

private ExecutionResult FromRepositoryValidation(EngineeringRequest request)
{
    var repositoryContext = RequireRepositoryContext(request);

    var skillContext = new RuntimeExecutionContext(
        "validation-" + Guid.NewGuid().ToString("N"),
        new Tier0Context(Array.Empty<string>(), Array.Empty<string>()),
        request.ProjectProfileId,
        "RepositoryValidation",
        new Dictionary<string, object> { ["RepositoryContext"] = repositoryContext });

    var invocation = _runtime.SkillHost.Invoke(skillContext, "SKL-EXE-0031-VALIDATION-PLACEHOLDER");
    // (see Task 14 — the real registered Id is "REPO-VALIDATION"'s bound Behavior, wired below)
    return new ExecutionResult(false, null, null, Array.Empty<string>(), null, TimeSpan.Zero, null, null);
}

private T? InvokeSkill<T>(string skillId, string contentKey, object payload, string projectProfileId, string tierLabel) where T : class
{
    var skillContext = new RuntimeExecutionContext(
        tierLabel + "-" + Guid.NewGuid().ToString("N"),
        new Tier0Context(Array.Empty<string>(), Array.Empty<string>()),
        projectProfileId,
        tierLabel,
        new Dictionary<string, object> { [contentKey] = payload });

    var invocation = _runtime.SkillHost.Invoke(skillContext, skillId);
    return invocation.Outcome == SkillInvocationOutcome.Invoked ? invocation.Output as T : null;
}
```

**Correction before implementing** — `FromRepositoryValidation` above is deliberately left inconsistent (a placeholder Skill Id) because the real registered Id for the validation Behavior is decided in Task 14's `Program.cs` wiring, not here. Register `RepositoryValidationSkillBehavior` under Skill Id **`"REPO-VALIDATION"`** directly (an implementation-only sentinel Id is exactly as valid a `SkillHost` dictionary key as a governed `SKL-EXE-NNNN` Id — `SkillHost.Invoke` only requires a Registry-registered, Active definition; register `"REPO-VALIDATION"` in the manifest as an `ArchitectureArtifact("REPO-VALIDATION", "Execution")` alongside Task 4's other additions). Rewrite `FromRepositoryValidation` to invoke `"REPO-VALIDATION"` directly:

```csharp
private ExecutionResult FromRepositoryValidation(EngineeringRequest request)
{
    var repositoryContext = RequireRepositoryContext(request);
    var invocation = InvokeSkill<ValidationOutcome>("REPO-VALIDATION", "RepositoryContext", repositoryContext, request.ProjectProfileId, "RepositoryValidation");

    if (invocation is null)
    {
        return new ExecutionResult(false, null, "AGT-EXE-0015", new[] { "REPO-VALIDATION is not invocable." }, null, TimeSpan.Zero, null, null);
    }

    var diagnostics = invocation.IsEscalated ? new[] { invocation.EscalationReason! } : Array.Empty<string>();
    return new ExecutionResult(!invocation.IsEscalated, null, "AGT-EXE-0015", diagnostics, null, TimeSpan.Zero, null, null,
        RepositoryAnalyzed: repositoryContext.RepositoryName, TestsExecuted: !invocation.IsEscalated);
}
```

(Add `using Runtime.DomainContent.DeliveryExecution;` for `ValidationOutcome`, and correct Task 4's manifest entry list to include `new ArchitectureArtifact("REPO-VALIDATION", "Execution")` — 35 + 1 = 36 total; update Task 4's own count and its test assertions accordingly before running Task 4's steps.)

- [ ] Update Task 4's manifest total to 36 and add the `REPO-VALIDATION` assertion (go back and amend Task 4's commit is not required — fold this into this task's own commit instead, since it is discovered here, not in Task 4; add the manifest entry + test assertion now).
- [ ] Implement `ConversationIntakeAndRoutingBehavior`'s new keyword entries and doc-comment/message update.
- [ ] Implement `EngineeringExecutionPipeline`'s `FromEngineeringPlanning`, `FromRepositoryIntelligence`, `FromRepositoryValidation`, `RequireRepositoryContext`, `InvokeSkill<T>` (using the corrected `FromRepositoryValidation` above, not the placeholder version).
- [ ] Update `ConversationIntakeAndRoutingBehaviorTests.cs`: add `[InlineData]` cases for `"Please analyze the repository"` → `WFL-DEL-0008`, `"Plan the new feature specification"` → `WFL-DEL-0006`, `"Validate the build"` → `REPO-VALIDATION`.
- [ ] Add new `EngineeringExecutionPipelineTests.cs` cases: `Execute_AnalyzeRequest_DispatchesToRepositoryIntelligence` (real temp git+dotnet repo via `Payload["RepositoryName"]`/`["RepositoryRoot"]`, bind all seven new Behaviors, assert `Success == true`, `ExecutedAgentId == "AGT-EXE-0015"`, `RepositoryAnalyzed` matches); `Execute_PlanRequest_DispatchesToEngineeringPlanning` (assert `ExecutedAgentId == "AGT-EXE-0012"`); `Execute_ValidateRequest_DispatchesToRepositoryValidation` (real temp dotnet repo, fake `IVerificationRunner` via a test-only Behavior construction path — bind `RepositoryValidationSkillBehavior` with a fake runner in the test's own boot, mirroring Task 12's test style).
- [ ] `dotnet build AefPlatform.sln` succeeds; `dotnet test AefPlatform.sln` full suite green.
- [ ] Commit: `feat(runtime): wire Engineering Planning, Repository Intelligence, and Repository Validation into EngineeringExecutionPipeline`.

---

## Task 14: Wire the live Host — register the 7 new Behaviors + `REPO-VALIDATION`

**Files:** Modify `src/Host/Program.cs`.

```csharp
using Runtime.DomainContent.EngineeringPlanning;
using Runtime.DomainContent.RepositoryIntelligence;
using Runtime.DomainContent.RepositoryValidation;

// Inside the existing behaviors dictionary literal, append:
["SKL-EXE-0023"] = new EngineeringDiscoveryAndGoalClarificationBehavior(),
["SKL-EXE-0024"] = new SpecificationDraftingAndAlignmentBehavior(),
["SKL-EXE-0025"] = new ReadinessClassificationAndApprovalRequestBehavior(),
["SKL-EXE-0030"] = new RepositoryAndArchitectureUnderstandingBehavior(),
["SKL-EXE-0031"] = new CodeIntelligenceAndQualitySignalDetectionBehavior(),
["SKL-EXE-0032"] = new EngineeringDiagnosticsAndRootCauseAnalysisBehavior(),
["SKL-EXE-0033"] = new RecommendationSynthesisAndEvidenceBasedReportingBehavior(),
["REPO-VALIDATION"] = new RepositoryValidationSkillBehavior(),
```

- [ ] Make the change.
- [ ] `dotnet build AefPlatform.sln` succeeds.
- [ ] Manual boot check: `cd src/Host && dotnet run --no-build`, confirm `Runtime Architecture Loader: 36/36 artifacts registered.` and clean startup.
- [ ] Commit: `feat(host): wire Engineering Planner, Repository Intelligence, and Repository Validation behaviors into the live Host`.

---

## Task 15: Live-Host end-to-end integration test, final verification, and documentation

**Files:** Create `tests/Host.Tests/RepositoryEngineeringHostIntegrationTests.cs`; modify `README.md`.

Mirror `EngineeringExecutionPipelineHostIntegrationTests`' own `CreateBuilder` pattern exactly (P2 Task 10), wiring all twelve behaviors (five from P2 + seven new), then:

```csharp
[Fact]
public async Task Execute_AnalyzeRequest_AgainstTheRealBootedHost_DispatchesToRepositoryIntelligence()
{
    // ... same builder/host setup as EngineeringExecutionPipelineHostIntegrationTests,
    // extended with the 7 new behaviors from Task 14 ...

    var root = Directory.CreateTempSubdirectory().FullName;
    try
    {
        // git init + a .csproj marker file, matching Task 3's own test setup
        var pipeline = host.Services.GetRequiredService<EngineeringExecutionPipeline>();
        var result = pipeline.Execute(new EngineeringRequest(
            "Please analyze the repository", "default-profile",
            new Dictionary<string, object> { ["RepositoryName"] = "test-repo", ["RepositoryRoot"] = root }));

        Assert.True(result.Success);
        Assert.Equal("AGT-EXE-0015", result.ExecutedAgentId);
        Assert.Equal("test-repo", result.RepositoryAnalyzed);
    }
    finally
    {
        Directory.Delete(root, true);
    }
}
```

- [ ] Write the test, confirm it passes (or fix the earlier task's wiring if it doesn't — the fix belongs in that task's own file, not here).
- [ ] `dotnet build AefPlatform.sln` — 0 warnings, 0 errors.
- [ ] `dotnet test AefPlatform.sln` — full suite green, record the total test count.
- [ ] Manual boot: `cd src/Host && dotnet run --no-build`, confirm `36/36 artifacts registered` and no error-level logs.
- [ ] Update `README.md`'s `## Status` section (new entry above the P2 entry, preserving every existing entry) summarizing: the Repository/Git Operations Integration Adapters, `RepositoryExecutionContext`, the Engineering Planner (`AGT-EXE-0012`) and Repository Intelligence (`AGT-EXE-0015`) deterministic Behaviors, `BuildSystemDetector`/`RepositoryValidationSkillBehavior`, the pipeline's three new dispatch paths, and the explicit remaining gap: genuine NL/LLM-backed reasoning for specification prose authoring and root-cause causal inference remains out of scope (no LLM integration point exists anywhere in this Runtime) — `AGT-EXE-0013`/`0014` remain the next documented gap, unchanged from P2.
- [ ] Commit: `docs: record P3 (Repository Engineering Engine) completion`.

## Definition of Done Evidence Map

| P3 DoD Item | Evidence |
|---|---|
| A repository can be discovered | Task 3: `RepositoryContextFactory.Create` (reuses `LocalFileSystemRepositoryScanner`) |
| Repository metadata can be read | Task 3: `RepositoryExecutionContext` (branch, working-tree state, Technology Profile) |
| Repository operations execute successfully | Task 1: `LocalFileSystemRepositoryOperations` |
| Git operations execute successfully | Task 2: `LocalGitOperations` |
| Repository intelligence produces structured results | Tasks 5-8: `SKL-EXE-0030/0031/0032/0033` Behaviors |
| Engineering planning integrates with repository analysis | Tasks 9-11: `SKL-EXE-0023/0024/0025`, `DiscoveryRequest.RepositoryContext` |
| Validation executes successfully | Task 12: `RepositoryValidationSkillBehavior` (reuses `DeliveryValidationBehavior`/`IVerificationRunner`) |
| Structured engineering results are returned | Task 13: `ExecutionResult`'s three new fields |
| Automated tests pass | Task 15 full-suite run |
| Working tree clean / all work committed | Task 15 |
