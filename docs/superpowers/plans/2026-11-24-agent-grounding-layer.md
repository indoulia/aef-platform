# Agent Grounding Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give coding agents two MCP tools, `aef_refine_prompt` and `aef_check_change`, backed by tokensave, and prove the whole platform end to end on Ferret, marksy-api and marksy-os.

**Architecture:** Two new capabilities ride the existing path: MCP tool → fixed routing literal → `EngineeringExecutionPipeline` dispatch chain → skill behaviors. Repository facts come from a new `ICodeIntelligenceProvider` whose only binding shells out to `tokensave tool`. The brief is assembled deterministically; AEF calls no LLM for it.

**Tech Stack:** .NET (existing solution `AefPlatform.sln`), xUnit, ModelContextProtocol SDK (already referenced), `tokensave` CLI 7.x, `git`, `claude` CLI 2.x.

**Spec:** `docs/superpowers/specs/2026-11-24-agent-grounding-layer-design.md`

## Global Constraints

- AEF never edits, commits to, or indexes a target repository. It does not run `tokensave init` on the caller's behalf.
- A line may appear under "Verified facts" only if it has a non-empty `File`. Everything else goes to Assumptions or Open questions.
- Everything works with no AI provider configured and with tokensave absent.
- `Adapters.*` reference `Runtime.Agent` only; `Runtime.Agent` references `Runtime` only (existing fitness tests must stay green).
- No new NuGet dependency.
- Contract changes are additive only (new nullable trailing members).
- Capability ids are the non-governed literals `PROMPT-GROUNDING` and `CHANGE-CHECK`, following the existing `REPO-VALIDATION` precedent. No governance documents or epic are created by this plan.
- Brief cap: `MaxBriefCharacters = 6000`, `MaxWhereToWork = 12`.
- External process calls use `ProcessStartInfo.ArgumentList`, read stdout and stderr concurrently, and time out at 30 seconds.
- Work on branch `feature/agent-grounding-layer`; one commit per task; stage `.claude/` files with each commit.
- Not in scope: the three deferred MCP tools, pipeline dispatch refactor (issue #24), auth, persistence, remote hosting, package install.

## Review Focus

1. The prompt names a file or symbol that does not exist → it appears under Open questions as not found, never under Verified facts (Task 5).
2. tokensave is not installed, or the repository is not indexed → a brief is still returned and says code intelligence is unavailable and why (Tasks 4, 5).
3. `RepositoryRoot` is not a git repository, has no commits, or has no detectable default branch → `aef_check_change` reports that plainly and does not throw (Task 7).
4. tokensave hangs or the repository is very large → the call times out at 30 seconds and degrades to "unavailable"; the brief stays within 6000 characters (Tasks 4, 5).
5. Paths with spaces, Windows backslashes, and prompts containing quotes or newlines → arguments survive intact and every file reference is emitted with forward slashes (Tasks 4, 6).
6. Empty or whitespace `UserPrompt` → a failed result asking for a prompt, not an empty brief (Task 6).

---

### Task 1: Fix the `aef-mcp` connection failure

**Files:**
- Modify: whatever the diagnosis identifies (likely `src/Mcp/Program.cs` or the MCP registration in the user's Claude Code config)
- Test: `tests/Mcp.Tests/McpExecutableStartupTests.cs`

- [ ] **Step 1: Reproduce.** Use superpowers:systematic-debugging. Run `claude mcp get aef-mcp` to get the registered command, run that exact command by hand, and send it an MCP `initialize` request on stdin. Record the exit code, stderr, and anything written to stdout before the JSON-RPC response.
- [ ] **Step 2: Classify the cause.** Registration problem (wrong path, unbuilt output, stale Debug/Release) or product defect (non-protocol bytes on stdout, startup exception, missing `appsettings.json` next to the dll).
- [ ] **Step 3: If it is a product defect, write a failing test** in `McpExecutableStartupTests` that launches the built executable the way the registration does and asserts the first stdout line parses as a JSON-RPC response with `result.serverInfo`.
- [ ] **Step 4: Fix, then run** `dotnet test tests/Mcp.Tests`. Expected: all pass.
- [ ] **Step 5: Verify live.** `claude mcp list` shows `aef-mcp` connected.
- [ ] **Step 6: Commit** `fix: aef-mcp server fails to connect`.

### Task 2: Carry evidence to the caller

Today `Execute` overwrites `PresentedResult` with the handoff text, and the MCP result returns only that text, so an agent sees "Completed via WFL-DEL-0008." and nothing else.

**Files:**
- Modify: `src/Runtime/Execution/ExecutionResult.cs`, `src/Runtime.Agent/ExecutionEvidence.cs`, `src/Runtime.Agent/AgentExecutionService.cs`, `src/Adapters.Mcp/McpToolInvoker.cs`
- Test: `tests/Runtime.Agent.Tests/`, `tests/Adapters.Mcp.Tests/McpToolInvokerTests.cs`, `tests/Adapters.Rest.Tests/ContractCompatibilityTests.cs`

**Interfaces:**
- Produces: `ExecutionResult.Document` (`string?`, new trailing named parameter, default `null`); `ExecutionEvidence.Document` (`string?`, serialized as `document`); `McpToolInvoker.BuildCallToolResult(ExecutionResponse)` text layout below.

- [ ] **Step 1: Write failing tests.**

```csharp
[Fact] public void BuildCallToolResult_WithDocument_ReturnsPresentedResultThenDocument()
// response.PresentedResult = "Completed via PROMPT-GROUNDING.", Evidence.Document = "# Brief\nbody"
// Assert text == "Completed via PROMPT-GROUNDING.\n\n# Brief\nbody"

[Fact] public void BuildCallToolResult_WithoutDocument_AppendsDiagnosticsAndValidationResults()
// Diagnostics = ["[Pass] Test Convention: found"], ValidationResults = ["answer"]
// Assert text contains "\n\nFindings:\n- [Pass] Test Convention: found" and "\n\nResults:\n- answer"

[Fact] public void BuildCallToolResult_Failed_ReturnsReasonThenDiagnostics_IsErrorTrue()

[Fact] public void Execute_MapsExecutionResultDocument_ToEvidenceDocument()   // Runtime.Agent.Tests
```

- [ ] **Step 2: Run** `dotnet test tests/Adapters.Mcp.Tests --filter FullyQualifiedName~BuildCallToolResult`. Expected: FAIL (no `Document` member).
- [ ] **Step 3: Implement** the two new members, the mapping in `AgentExecutionService`, and the layout in `BuildCallToolResult`. Empty lists produce no heading.
- [ ] **Step 4: Update** `ContractCompatibilityTests` to assert `document` is present and is `Null` or `String`; update the Story S4 assertions that pin exact MCP result text.
- [ ] **Step 5: Run** `dotnet test AefPlatform.sln` (shared contract change). Expected: all pass.
- [ ] **Step 6: Commit** `feat: return evidence in MCP tool results`.

### Task 3: Story S8 — real-repository sweep of the existing surface

**Files:**
- Create: `docs/reliability/AEF-v1.0-MCP-Dogfooding-Sweep-2026-11.md`

- [ ] **Step 1: Run each of the 8 in-scope MCP tools plus `aef_health` and `aef_version`** through the real built `AefPlatform.Mcp` executable against `C:\AIAgent\Ferret`, `C:\AIAgent\marksy-api` and `C:\AIAgent\marksy-os`, using temporary, never-committed tests on the existing `Mcp.Tests` subprocess harness (the RC0/GA-3 precedent). Call the three deferred tools once each and confirm the "deferred" message.
- [ ] **Step 2: Repeat the same requests over CLI and REST** for one repository and confirm the three channels return the same evidence.
- [ ] **Step 3: For every unhandled error or misleading result,** root-cause it, add a regression test in the owning test project, fix it, and rerun that project's tests.
- [ ] **Step 4: Write the report:** requests, verbatim results, defects found and fixed, anything logged and not fixed.
- [ ] **Step 5: Delete the temporary tests,** confirm `git status` shows only intended files, and commit `docs: MCP adapter real-repository sweep (Epic M2-0001 Story S8)`.

### Task 4: Code-intelligence seam and tokensave binding

**Files:**
- Create: `src/Runtime/Integration/CodeIntelligence/ICodeIntelligenceProvider.cs`, `CodeReference.cs`, `CodeLookupResult.cs`, `TokensaveCodeIntelligenceProvider.cs`
- Test: `tests/Runtime.Tests/Integration/CodeIntelligence/TokensaveCodeIntelligenceProviderTests.cs`, fixtures under `tests/Runtime.Tests/Integration/CodeIntelligence/Fixtures/`

**Interfaces:**
- Produces:

```csharp
namespace Runtime.Integration.CodeIntelligence;

public sealed record CodeReference(string Name, string Kind, string File, int Line, string? Signature, string Source);

public sealed record CodeLookupResult(bool Available, string? UnavailableReason, IReadOnlyList<CodeReference> References)
{
    public static CodeLookupResult Unavailable(string reason);
}

public interface ICodeIntelligenceProvider
{
    CodeLookupResult FindRelevant(string repositoryRoot, string taskDescription, int maxResults);
    CodeLookupResult FindSymbol(string repositoryRoot, string name, int maxResults);
    CodeLookupResult FindTests(string repositoryRoot, string file);
    CodeLookupResult FindAffected(string repositoryRoot, IReadOnlyList<string> changedFiles);
}

public delegate ProcessOutput ProcessRunner(string workingDirectory, string fileName, IReadOnlyList<string> arguments, TimeSpan timeout);
public sealed record ProcessOutput(int ExitCode, string StandardOutput, string StandardError, bool TimedOut, bool NotFound);

public sealed class TokensaveCodeIntelligenceProvider : ICodeIntelligenceProvider
{
    public TokensaveCodeIntelligenceProvider();                    // real process runner
    public TokensaveCodeIntelligenceProvider(ProcessRunner runner); // tests
}
```

`Source` is always `"tokensave"` for this binding. `File` is repository-relative with forward slashes. The spec's "callers/dependents" operation is intentionally omitted; nothing in this plan consumes it.

- [ ] **Step 1: Capture real output.** In this repository run `tokensave tool search McpToolInvoker` (known to return a JSON array of `{id,name,kind,file,line,signature,score}`), then `tokensave tool context`, `tokensave tool test_map` and `tokensave tool affected` with the argument forms `tokensave tool <name>` accepts. Save each stdout verbatim as a fixture file. Also capture the output when run in a directory with no index.
- [ ] **Step 2: Write failing tests** using a fake `ProcessRunner` that returns the fixtures.

```csharp
[Fact] public void FindSymbol_ParsesSearchFixture_ReturnsReferencesWithFileAndLine()
// first reference: Name "McpToolInvoker", Kind "class", File "src/Adapters.Mcp/McpToolInvoker.cs", Line 25, Source "tokensave"
[Fact] public void FindSymbol_RespectsMaxResults()
[Fact] public void AnyOperation_ToolNotFound_ReturnsUnavailable_NamingTokensave()
[Fact] public void AnyOperation_NotIndexed_ReturnsUnavailable_TellingCallerToRunTokensaveInit()
[Fact] public void AnyOperation_TimedOut_ReturnsUnavailable()
[Fact] public void AnyOperation_UnparseableOutput_ReturnsUnavailable_NeverThrows()
[Fact] public void FindSymbol_NormalizesBackslashesToForwardSlashes()
[Fact] public void FindRelevant_PassesTaskDescriptionWithQuotesAndNewlines_AsOneArgument()
```

- [ ] **Step 3: Run** `dotnet test tests/Runtime.Tests --filter FullyQualifiedName~TokensaveCodeIntelligenceProvider`. Expected: FAIL (types missing).
- [ ] **Step 4: Implement.** The real runner follows `LocalGitOperations`' process pattern with the Global Constraints' process rules.
- [ ] **Step 5: Add one real-process test** `FindSymbol_AgainstThisRepository_FindsEngineeringExecutionPipeline`, skipped when `tokensave` is not on PATH.
- [ ] **Step 6: Run the filter again.** Expected: PASS. Commit `feat: code-intelligence seam with tokensave binding`.

### Task 5: Prompt grounding skill and brief renderer

**Files:**
- Create: `src/Runtime/DomainContent/PromptGrounding/PromptGroundingTypes.cs`, `PromptIdentifierExtractor.cs`, `PromptGroundingSkillBehavior.cs`, `GroundedBriefRenderer.cs`
- Test: `tests/Runtime.Tests/DomainContent/PromptGrounding/`

**Interfaces:**
- Consumes: `ICodeIntelligenceProvider`, `CodeReference`, `CodeLookupResult` (Task 4); existing `RepositoryExecutionContext`, `BuildSystemDetector`, `IBehavior`.
- Produces:

```csharp
namespace Runtime.DomainContent.PromptGrounding;

public sealed record PromptGroundingRequest(string UserPrompt, RepositoryExecutionContext Repository);
public sealed record EvidencedFact(string Statement, string File, int? Line, string Source);
public sealed record GroundedBrief(
    string Goal,
    IReadOnlyList<EvidencedFact> VerifiedFacts,
    IReadOnlyList<CodeReference> WhereToWork,
    IReadOnlyList<string> TestFiles,
    string? BuildAndTestCommand,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<string> AcceptanceChecks,
    int OmittedCount);

public static class PromptIdentifierExtractor { public static IReadOnlyList<string> Extract(string prompt); }
public static class GroundedBriefRenderer { public const int MaxBriefCharacters = 6000; public static string Render(GroundedBrief brief); }

public sealed class PromptGroundingSkillBehavior : IBehavior   // reads Tier3SkillContent["PromptGroundingRequest"], returns GroundedBrief
{
    public const int MaxWhereToWork = 12;
    public PromptGroundingSkillBehavior(ICodeIntelligenceProvider provider);
}
```

Decisions the tests do not fully pin:
- `Extract` returns, de-duplicated and in order: backticked spans, tokens containing `/`, `\` or a file extension, and `PascalCase`/`camelCase`/`snake_case` tokens of two or more parts. Plain English words are not returned.
- For each extracted identifier, `FindSymbol`; a file-like identifier is also checked on disk under the repository root. Found → a Verified fact `"<name> is a <kind>"` with its file and line. Not found → Open question `"The prompt mentions `<name>`, which was not found in the repository. Which file or symbol did you mean?"`.
- `WhereToWork` is `FindRelevant(prompt)` plus the found identifiers, ranked identifiers first, capped at `MaxWhereToWork`; the remainder count goes to `OmittedCount`.
- `TestFiles` is `FindTests` for each distinct `WhereToWork` file.
- Stack and build command come from the existing `RepositoryExecutionContext.TechnologyProfile` and `BuildSystemDetector`. A stack item with no evidencing file goes to Assumptions.
- Provider unavailable → Assumption `"Code intelligence unavailable: <reason>. File and symbol locations below were not verified."`.
- A prompt with no extracted identifier and no relevant result → Open question `"The prompt does not name a file, symbol or feature that could be located. What part of the repository is this about?"`.
- `AcceptanceChecks` always: `"`<command>` exits 0"` when a command is known; `"Changes are confined to the files under Where to work, or each extra file is explained"`; `"Every open question above is answered before editing"`; plus `"Tests in <test file> pass"` per test file.
- `Render` emits the sections in spec order with headings `## Goal`, `## Verified facts`, `## Where to work`, `## How to build and test`, `## Assumptions`, `## Open questions`, `## Acceptance checks`, `## Refined prompt`, then the closing line `Treat Verified facts as ground truth. Ask the user the Open questions before editing.` Each fact renders as `- <statement> (<file>:<line>)`. An empty section renders `- None.` Over the cap, `WhereToWork` then `TestFiles` are trimmed from the end and a line `(<n> more omitted)` is added.
- `Goal` is the trimmed prompt, first 300 characters. "Refined prompt" is the Goal followed by the Where to work files, the build command, and the acceptance checks as one paragraph each.

- [ ] **Step 1: Write failing tests** with a fake provider.

```csharp
[Fact] public void Extract_ReturnsBacktickedPathsAndCompoundIdentifiers_NotPlainWords()
// "Fix the retry in `OrderService.Submit` and src/api/orders.ts please" → ["OrderService.Submit", "src/api/orders.ts"]
[Fact] public void Execute_IdentifierFound_BecomesVerifiedFactWithFileAndLine()
[Fact] public void Execute_IdentifierNotFound_BecomesOpenQuestion_NotAFact()
[Fact] public void Execute_ProviderUnavailable_ReturnsBriefWithAssumptionAndNoCodeFacts()
[Fact] public void Execute_EveryVerifiedFact_HasNonEmptyFile()
[Fact] public void Execute_MoreThanTwelveRelevant_CapsWhereToWorkAndCountsOmitted()
[Fact] public void Execute_NothingLocated_AsksWhatPartOfTheRepository()
[Fact] public void Render_ContainsAllEightHeadingsInOrder_AndClosingLine()
[Fact] public void Render_LargeBrief_StaysWithinMaxBriefCharacters_AndStatesOmittedCount()
[Fact] public void Render_FactLine_HasFileColonLine()
```

- [ ] **Step 2: Run** `dotnet test tests/Runtime.Tests --filter FullyQualifiedName~PromptGrounding`. Expected: FAIL.
- [ ] **Step 3: Implement** the four files.
- [ ] **Step 4: Run the filter.** Expected: PASS. Commit `feat: prompt grounding skill and brief renderer`.

### Task 6: Wire `aef_refine_prompt` through all three channels

**Files:**
- Modify: `src/Runtime.Agent/Capabilities/EngineeringAgentCapabilityCatalog.cs`, `src/Runtime/DomainContent/ConversationManagement/ConversationIntakeAndRoutingBehavior.cs`, `src/Runtime/Execution/EngineeringExecutionPipeline.cs`, `src/Composition/EngineeringArchitectureManifest.cs`, `src/Host/Program.cs`, `src/Cli/Program.cs`, `src/Mcp/Program.cs`, `src/Adapters.Mcp/McpToolCatalog.cs`, `src/Adapters.Mcp/McpToolInvoker.cs`
- Test: `tests/Runtime.Tests/`, `tests/Adapters.Mcp.Tests/`, `tests/Mcp.Tests/`, `tests/Cli.Tests/`, `tests/Adapters.Rest.Tests/`

**Interfaces:**
- Consumes: `PromptGroundingSkillBehavior`, `GroundedBriefRenderer` (Task 5); `ExecutionResult.Document` (Task 2).
- Produces: capability `PROMPT-GROUNDING`, name `"Prompt Grounding"`, required payload keys `UserPrompt`, `RepositoryName`, `RepositoryRoot`; MCP tool `aef_refine_prompt`; routing literal `"refine prompt"`.

- [ ] **Step 1: Write failing tests.**

```csharp
[Fact] public void Route_RefinePrompt_TargetsPromptGrounding()                       // Runtime.Tests
[Theory] /* every literal in McpToolInvoker's table */ public void ExistingRoutingLiterals_StillRouteToTheirOwnCapability(...)
[Fact] public void Execute_PromptGrounding_ReturnsSuccessWithDocumentContainingVerifiedFactsHeading()
[Fact] public void Execute_PromptGrounding_EmptyUserPrompt_FailsAskingForAPrompt()
[Fact] public void Execute_PromptGrounding_NoRepositoryPayload_ReturnsMissingRepositoryContext()
[Fact] public void BuildExecutionRequest_RefinePrompt_UsesFixedLiteral_AndKeepsUserPromptInPayload()   // Adapters.Mcp.Tests
[Fact] public async Task ToolsCall_RefinePrompt_OverRealSubprocess_ReturnsBriefText()                 // Mcp.Tests, temp fixture repo
[Fact] public async Task ToolsCall_RefinePrompt_RepositoryRootWithSpaces_Succeeds()
```

Update the existing tools/list count assertion from 13 to 14.

- [ ] **Step 2: Run** the three affected projects. Expected: new tests FAIL.
- [ ] **Step 3: Implement.**
  - Append `("refine prompt", "PROMPT-GROUNDING")` as the last row of the routing table.
  - Add `"PROMPT-GROUNDING" => FromPromptGrounding(request)` to `Dispatch`. `FromPromptGrounding` follows `FromRepositoryValidation`'s shape: workflow id `null`, agent id `"AGT-EXE-0015"`, `Diagnostics` = the brief's open questions, `ValidationResults` = the acceptance checks, `Document` = `GroundedBriefRenderer.Render(brief)`, `RepositoryAnalyzed` set. The prompt is read from `Payload["UserPrompt"]`, never from `UserRequest`.
  - Register `["PROMPT-GROUNDING"] = new PromptGroundingSkillBehavior(new TokensaveCodeIntelligenceProvider())` in all three composition roots, and add the manifest entry mirroring `REPO-VALIDATION`'s.
  - Catalog description: `"Turns a user prompt into a grounded brief for a coding agent: verified repository facts with file and line, where to work, build and test commands, flagged assumptions, open questions, and acceptance checks. Reads only; never modifies the repository."`
- [ ] **Step 4: Run** `dotnet test AefPlatform.sln` (routing table and manifest are shared). Expected: all pass, including the fitness tests.
- [ ] **Step 5: Commit** `feat: aef_refine_prompt capability on MCP, CLI and REST`.

### Task 7: `aef_check_change`

**Files:**
- Modify: `src/Runtime/Integration/GitOperations/IGitOperations.cs`, `LocalGitOperations.cs`, and the same wiring files as Task 6
- Create: `src/Runtime/Integration/GitOperations/GitChangedFilesResult.cs`, `src/Runtime/DomainContent/ChangeCheck/ChangeCheckTypes.cs`, `ChangeCheckSkillBehavior.cs`, `ChangeCheckRenderer.cs`
- Test: `tests/Runtime.Tests/Integration/GitOperations/`, `tests/Runtime.Tests/DomainContent/ChangeCheck/`, `tests/Mcp.Tests/`

**Interfaces:**
- Consumes: `ICodeIntelligenceProvider.FindAffected` (Task 4); existing `REPO-VALIDATION` skill and `ValidationOutcome`; `ExecutionResult.Document` (Task 2).
- Produces:

```csharp
public sealed record GitChangedFilesResult(bool Succeeded, string? FailureReason, string? ResolvedBaseRef, IReadOnlyList<string> Files);
// on IGitOperations:
GitChangedFilesResult ChangedFilesSince(string? baseRef);

namespace Runtime.DomainContent.ChangeCheck;
public sealed record ChangeCheckRequest(RepositoryExecutionContext Repository, GitChangedFilesResult Changes, ValidationOutcome? Validation,
    IReadOnlyList<string> AcceptanceChecks, IReadOnlyList<string> ExpectedFiles);
public sealed record AcceptanceCheckResult(string Check, string Status, string Evidence);   // Status: "met" | "not met" | "cannot tell"
public sealed record ChangeCheckReport(IReadOnlyList<string> FilesChanged, IReadOnlyList<string> FilesOutsideExpected,
    IReadOnlyList<CodeReference> Affected, bool? BuildAndTestsPassed, string? BuildAndTestDetail, IReadOnlyList<AcceptanceCheckResult> Checks, IReadOnlyList<string> Notes);
public sealed class ChangeCheckSkillBehavior : IBehavior { public ChangeCheckSkillBehavior(ICodeIntelligenceProvider provider); }   // content key "ChangeCheckRequest"
public static class ChangeCheckRenderer { public static string Render(ChangeCheckReport report); }
```

Capability `CHANGE-CHECK`, name `"Change Check"`, required keys `RepositoryName`, `RepositoryRoot`; MCP tool `aef_check_change`; routing literal `"check change"`. Optional payload keys `BaseRef`, `AcceptanceChecks` and `ExpectedFiles` are plain strings, the last two newline-separated, and are named in the catalog description because the MCP input schema lists required keys only.

Decisions:
- `ChangedFilesSince(null)` uses the merge-base of `HEAD` with the default branch; if none can be resolved it compares against `HEAD` and the report notes that only uncommitted changes were examined. Result = committed changes since the base ∪ staged ∪ unstaged ∪ untracked, forward slashes, de-duplicated.
- A check is evaluated only when it is one of Task 5's generated forms: the command check uses the validation outcome; the "confined to" check uses `FilesOutsideExpected`; a "Tests in <file> pass" check follows the validation outcome. Any other text is `"cannot tell"` with evidence `"Not machine-checkable; needs human or agent review."`
- No changed files → the report says so and every check is `"cannot tell"`.
- The pipeline chain `FromChangeCheck` runs `REPO-VALIDATION` first, then `CHANGE-CHECK`. `Success` is `false` only when the build/test run completed and failed, or git could not be read. Tool unavailable or not installed is a Note, not a failure.

- [ ] **Step 1: Write failing tests.**

```csharp
[Fact] public void ChangedFilesSince_Null_OnFeatureBranch_ReturnsCommittedStagedUnstagedAndUntracked()   // real temp git repo
[Fact] public void ChangedFilesSince_NotAGitRepository_ReturnsFailedWithReason_NeverThrows()
[Fact] public void ChangedFilesSince_RepositoryWithNoCommits_ReturnsFailedWithReason()
[Fact] public void ChangedFilesSince_NoDefaultBranchResolvable_FallsBackToHead_AndSaysSo()
[Fact] public void Execute_FileOutsideExpected_IsListed_AndConfinedCheckIsNotMet()
[Fact] public void Execute_ValidationPassed_CommandCheckIsMet()
[Fact] public void Execute_ValidationEscalated_CommandCheckIsCannotTell()
[Fact] public void Execute_FreeTextCheck_IsCannotTell()
[Fact] public void Execute_NoChanges_SaysNoChanges()
[Fact] public void Route_CheckChange_TargetsChangeCheck()
[Fact] public async Task ToolsCall_CheckChange_OverRealSubprocess_ListsTheChangedFile()   // Mcp.Tests, temp git repo with one edit
```

Update the tools/list count assertion from 14 to 15.

- [ ] **Step 2: Run** `dotnet test tests/Runtime.Tests --filter "FullyQualifiedName~ChangeCheck|FullyQualifiedName~ChangedFilesSince"`. Expected: FAIL.
- [ ] **Step 3: Implement,** wiring exactly as Task 6 Step 3 did.
- [ ] **Step 4: Run** `dotnet test AefPlatform.sln`. Expected: all pass.
- [ ] **Step 5: Commit** `feat: aef_check_change capability`.

### Task 8: `ClaudeCliProvider`

**Files:**
- Create: `src/Runtime.Ai/Providers/ClaudeCliProvider.cs`
- Modify: `src/Composition/AiProviderOptions.cs`, `src/Host/Program.cs`, `src/Cli/Program.cs`, `src/Mcp/Program.cs`, `src/Host/AiProviderReachabilityHealthContributor.cs`
- Test: `tests/Runtime.Ai.Tests/ClaudeCliProviderTests.cs`

**Interfaces:**
- Produces: `public sealed class ClaudeCliProvider : IAiProvider` with `Name => "ClaudeCli"`, constructors `()` and `(Func<string, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> run)`. Selected by `Ai:Provider = "ClaudeCli"`; no key or endpoint options are required for it.

- [ ] **Step 1: Capture real output** of `claude -p --output-format json` with a one-line prompt on stdin; save it as a fixture and note which field holds the reply text and which hold token usage.
- [ ] **Step 2: Write failing tests:** `CompleteAsync_ParsesReplyTextFromFixture`, `CompleteAsync_NonZeroExit_ThrowsAiProviderException`, `CompleteAsync_BinaryMissing_ThrowsAiProviderException`, `CompleteAsync_HonorsCancellation`, and an options test that `Provider = "ClaudeCli"` validates with no OpenAI section.
- [ ] **Step 3: Run** `dotnet test tests/Runtime.Ai.Tests --filter FullyQualifiedName~ClaudeCli`. Expected: FAIL.
- [ ] **Step 4: Implement.** System and user prompt are sent on stdin, not as arguments. Add the `"ClaudeCli"` arm to the provider switch in all three composition roots. The health contributor reports Healthy for this provider when `claude --version` exits 0.
- [ ] **Step 5: Run** `dotnet test tests/Runtime.Ai.Tests tests/Host.Tests`. Expected: PASS. Commit `feat: ClaudeCli AI provider`.

### Task 9: Real-repository sweep of the new tools and the LLM-backed skills

**Files:**
- Modify: `docs/reliability/AEF-v1.0-MCP-Dogfooding-Sweep-2026-11.md`

- [ ] **Step 1: Index the three test repositories** with `tokensave init` in each, and add `.tokensave` to each repository's `.git/info/exclude` (not its `.gitignore`).
- [ ] **Step 2: Refine sweep.** For each repository, take the subjects of five recent non-merge commits as loose prompts and call `aef_refine_prompt` over MCP. For every brief: open each Verified fact's `file:line` and confirm it says what the fact claims; record brief length and wall-clock time. Any fact that does not hold is a defect.
- [ ] **Step 3: Unavailable paths.** Repeat one prompt per repository with tokensave temporarily off PATH, and one against a non-indexed clone.
- [ ] **Step 4: Check sweep.** For each of the same commits, check out its parent in a throwaway worktree, apply the commit, and call `aef_check_change` with the brief's acceptance checks and Where to work files. Confirm the changed-file list equals `git diff --name-only`.
- [ ] **Step 5: LLM-backed skills.** With `Ai:Provider = "ClaudeCli"`, run `SKL-EXE-0026` through `0029` in-process via temporary tests, using a real specification document from one test repository and one of the commits above as the implementation. Record outputs verbatim and whether each finding is supported by its input.
- [ ] **Step 6: Fix what was found,** each with a regression test, then append results to the report, delete temporary tests and worktrees, and commit `docs: grounding-layer real-repository sweep`.

### Task 10: Agent-in-the-loop comparison

**Files:**
- Create: `docs/reliability/AEF-Agent-Grounding-Comparison-2026-11.md`, `tools/grounding-comparison/run.ps1`

- [ ] **Step 1: Fix the task set:** the 15 commits from Task 9. For each, the loose prompt is the commit subject and the starting point is the parent commit in a throwaway worktree.
- [ ] **Step 2: Write `run.ps1`** to run each task twice with headless Claude Code (`claude -p`): once with no MCP servers, once with only `aef-mcp` registered and one added instruction, `Call aef_refine_prompt first and aef_check_change when done.` It saves the transcript and final `git diff` per run. If `codex` is on PATH, do the same with its non-interactive mode; if not, record Codex as not run.
- [ ] **Step 3: Score each run** from the saved artefacts: (a) file paths or symbols in the transcript that do not exist in the worktree; (b) whether the detected build/test command exits 0 on the result; (c) number of questions the agent asked instead of proceeding; (d) files changed that the real commit did not touch. Headless runs cannot measure live user rounds, so (a)–(d) are the proxy; say so in the report.
- [ ] **Step 4: Write the report** with per-task results for both arms and totals, and state plainly whether AEF helped, made no difference, or hurt, per metric.
- [ ] **Step 5: Commit** `docs: agent grounding comparison`.

### Task 11: Documentation

**Files:**
- Modify: `README.md`, `docs/cli/cli-adapter-reference.md`, `docs/api/rest-adapter-reference.md`, `docs/guides/developer-guide.md`
- Create: `docs/mcp/mcp-adapter-reference.md`

- [ ] **Step 1: README.** Add a current-status entry at the top covering the MCP adapter, the two grounding tools, the tokensave dependency, and `ClaudeCli`; link the two reliability reports.
- [ ] **Step 2: MCP reference.** All 15 tools, their arguments (including the optional `aef_check_change` keys), an example brief, registration instructions for Claude Code and Codex, and the tokensave prerequisite (`tokensave init` in the target repository).
- [ ] **Step 3: CLI and REST references.** Add the two capabilities and the new `document` evidence field.
- [ ] **Step 4: Commit** `docs: grounding layer and MCP adapter reference`, then use superpowers:finishing-a-development-branch.

---

## Needs an explicit yes from the owner (not assumed)

- Adding capability, workflow, skill and agent documents and an epic for these two capabilities in `C:\AIAgent\ai-engineering-framework`, and replacing the two literal ids with governed ones.
