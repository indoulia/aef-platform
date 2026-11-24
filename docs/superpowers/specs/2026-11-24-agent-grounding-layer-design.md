# AEF Agent Grounding Layer — Design

Date: 2026-11-24. Status: owner answered the open questions on 2026-11-24 (see "Owner decisions"); proceeding to the implementation plan.

## Intent

AEF sits beside coding agents (Claude Code, Codex and similar) and makes them more accurate. It gives the agent verified facts about the repository and turns a loose user prompt into a precise, evidence-backed one, so the agent makes fewer wrong assumptions and the user needs fewer rounds. The agents stay as they are and keep doing the editing.

## Goals

1. An agent can call AEF over MCP and get a grounded brief for a user prompt.
2. An agent can call AEF after making a change and get an evidence-based check of that change.
3. Repository facts come from existing public tooling, not from new indexers written here.
4. The whole thing is proven end to end on real repositories with real agents, with a measured reduction in back-and-forth.
5. Epic M2-0001 is closed (Story S8) and the `aef-mcp` server connects reliably.

## Non-goals

- AEF editing code, committing, or executing its own migration/maintenance plans.
- Authentication, persistence, package-based install, remote hosting.
- Adding an LLM to all 11 existing capabilities.
- Un-deferring the three MCP tools that need Runtime-typed payloads (`aef_deliver_implementation`, `aef_review_specification`, `aef_validate_implementation`).
- Refactoring `EngineeringExecutionPipeline` dispatch (issue #24).

## Anti-goals

- A brief that states anything as fact without evidence. An unevidenced claim is a failure even if it happens to be true.
- A brief that costs the agent more tokens or time than reading the files itself would.
- A hard dependency on an LLM key. Everything must work without one.

## Constraints found in the code

- Every MCP tool call is routed through the keyword table in `ConversationIntakeAndRoutingBehavior` using a fixed per-tool literal (`McpToolInvoker`). There is no direct dispatch.
- `McpToolInvoker.BuildCallToolResult` returns only `PresentedResult` text. Evidence is dropped, so today an agent gets "Completed via WFL-DEL-0008." and nothing else.
- The behaviors dictionary is duplicated in three composition roots (`src/Host`, `src/Cli`, `src/Mcp`). A new skill must be added to all three and to `EngineeringArchitectureManifest`.
- `Adapters.*` may reference `Runtime.Agent` only; `Runtime.Agent` may reference `Runtime` only. Both are enforced by fitness tests.
- Capability and skill ids are governed in the separate `ai-engineering-framework` repository, which is not present at `../ai-engineering-framework` on this machine.
- `tokensave` and `node` are installed; `rg` and `ctags` are not on PATH.

## Approaches

1. **Fact tools only.** Small MCP tools over public code-intelligence tooling. Cheapest, but the agent has to choose to call them and the user prompt is never improved.
2. **Prompt-refinement front door (chosen).** One tool turns prompt + repository into a grounded brief; a second checks the change afterwards. Chosen because it targets the stated problem directly: wrong assumptions and repeated rounds start with an under-specified prompt.
3. **LLM in every capability.** Largest, and adds hallucination surface, which is the thing being removed.

## Design

### 1. Code-intelligence seam (my call)

New `src/Runtime/Integration/CodeIntelligence/ICodeIntelligenceProvider`, matching the existing Integration adapter pattern (`IGitOperations`, `IRepositoryScanner`). Operations: relevant symbols for a task description, symbol lookup by name, callers/dependents of a symbol, tests covering a file, files affected by a diff. Every result item carries `file`, `line`, and the tool that produced it.

First binding: `TokensaveCodeIntelligenceProvider`, shelling out to `tokensave tool <name>` the same way `LocalGitOperations` shells out to `git`. Reasons: it is already installed and used here, covers 34 languages, and has a CLI entry point for its MCP tools. When tokensave is missing or the repository is not indexed, the provider returns an explicit "unavailable" result and the brief says so; it never falls back to guessing.

Builder to verify before committing to this binding: the `tokensave tool` output format is machine-parseable, and its licence permits this use. If either fails, the fallback binding is `git grep` plus the existing `LocalFileSystemRepositoryScanner`, behind the same interface.

### 2. Refine tool

MCP tool `aef_refine_prompt`. Arguments: `UserPrompt`, `RepositoryName`, `RepositoryRoot`. The free-text prompt travels in the payload; the routing literal stays fixed, as for the other tools.

Returns a grounded brief as markdown in the tool result, with these sections:

- **Goal** — the request restated in one or two sentences.
- **Verified facts** — each line ends with `file:line`. Sources: code-intelligence provider, `StackDetector`, `BuildSystemDetector`, `LocalGitOperations`.
- **Where to work** — the files and symbols most relevant to the prompt, and the tests that cover them.
- **How to build and test** — the detected commands.
- **Assumptions** — anything inferred rather than found, labelled as such.
- **Open questions** — what the prompt leaves undecided and the repository cannot answer; the agent should ask the user these before editing.
- **Acceptance checks** — concrete, checkable conditions for done.
- **Refined prompt** — a paste-ready prompt assembled from the above.

Rule enforced in code and tested: a line can appear under Verified facts only if it has an evidence reference; otherwise it is moved to Assumptions or Open questions.

Assembly is fully deterministic; AEF calls no LLM for this tool. The calling agent is already an LLM, so any rewording is left to it, and the brief ends with one line telling the agent to treat Verified facts as ground truth and to ask the Open questions before editing.

Size cap (my call): the brief is limited to roughly 1,500 tokens, relevance-ranked, with a count of what was omitted.

### 3. Check tool

MCP tool `aef_check_change`. Arguments: `RepositoryName`, `RepositoryRoot`, optional `BaseRef` (default: merge-base with the default branch), optional `AcceptanceChecks` copied from the brief. Stateless (my call) so it survives process restarts and works across agents.

Returns: files changed, the build/test result from the existing Repository Validation path, symbols and tests affected by the diff, files changed outside the brief's "Where to work" set, and each acceptance check marked met / not met / cannot tell with evidence.

### 4. MCP result content

`BuildCallToolResult` is extended so evidence reaches the agent: the two new tools return their full markdown, and the existing eight tools append their diagnostics and validation results after `PresentedResult`. This changes existing tool output, so the Story S3/S4 assertions that pin it need updating.

### 5. Wiring

Two new capabilities in `EngineeringAgentCapabilityCatalog`, two new dispatch chains in `EngineeringExecutionPipeline`, new routing-table entries verified not to collide with existing literals, behaviors registered in all three composition roots, manifest entries added. Ids are placeholders until the governance question below is answered.

### 6. CLI and REST

They get the two capabilities automatically through the shared core. No channel-specific work beyond the reference docs.

## End-to-end testing

Four layers, each with its own evidence:

1. **Automated, CI-safe.** Subprocess tests using the existing `Mcp.Tests`, `CliExecutableFixture` and `WebApplicationFactory` harnesses against a temporary fixture repository. Covers both new tools on all three channels, the no-tokensave path, and the no-AI-key path.
2. **Real-repository sweep.** Every MCP tool, CLI command and REST endpoint run against real local repositories, following the 2026-07-09 sweep precedent, written up under `docs/reliability/`. This closes Story S8. Repositories (owner decision): `C:\AIAgent\Ferret`, `C:\AIAgent\marksy-api`, `C:\AIAgent\marksy-os`.
3. **Agent in the loop.** Claude Code and Codex with the AEF MCP server registered. A fixed set of about five realistic task prompts per repository, each run with and without AEF. Recorded per run: number of user rounds until an acceptable change, references to files or symbols that do not exist, and whether the first attempt builds and passes tests.
4. **LLM-backed skills, using the local Claude.** No API key is available, so a new `IAiProvider` binding, `ClaudeCliProvider`, shells out to the installed `claude -p` (my reading of "use the current Claude for the time being"). It is selected with `Ai:Provider = "ClaudeCli"` and lets the four existing AI-backed skills run against a real model for the first time. Those skills take Runtime-typed payloads no JSON channel can build, so this layer runs them in-process through temporary dogfooding tests, as RC0 and GA-3 did.

Success: layer 1 green; layer 2 with zero unhandled errors and no unevidenced fact in any brief; layer 3 showing fewer rounds and fewer nonexistent references with AEF than without, on a majority of tasks.

## Order of work

1. Diagnose and fix the `aef-mcp` connection failure.
2. MCP result content (section 4) and Story S8 sweep on the existing tools.
3. Code-intelligence seam and tokensave binding.
4. Refine tool.
5. Check tool.
6. Test layers 2–4 on the new tools; fix what they find.
7. Update README and the three channel reference docs.

## Left to the builder

- Exact method signatures on `ICodeIntelligenceProvider`.
- Relevance ranking and the precise token cap.
- Whether the MCP SDK's structured-content field is used in addition to markdown.
- Wording of routing literals.

## Owner decisions (2026-11-24)

1. The governance repository is at `C:\AIAgent\ai-engineering-framework`. Its pattern is one document each under `docs/framework/capabilities`, `workflows`, `skills` and `agents`, plus an epic under `docs/delivery/epics` (latest is `0014`). The Epic M2-0001 plan that PR #53 cites under `docs/06-history/` is not in the local checkout.
2. Test repositories: `Ferret`, `marksy-api`, `marksy-os`.
3. No API key; use the local Claude (see test layer 4).

## Still open

- Whether to add governance documents and a new epic for the two capabilities in `ai-engineering-framework`. Not assumed; the plan carries it as a step that needs an explicit yes.
