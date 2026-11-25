# AEF v1.0 — MCP and Grounding-Layer Real-Repository Sweep (2026-11-24)

Real requests through the real built binaries (`AefPlatform.Mcp.dll`, `AefPlatform.Cli.dll`, `AefPlatform.Host.dll`) against three real repositories: `C:\AIAgent\Ferret` (TypeScript), `C:\AIAgent\marksy-api` (Python) and `C:\AIAgent\marksy-os` (Android, Gradle Kotlin DSL). This closes Epic M2-0001 Story S8 and covers the two new grounding tools. The MCP server was driven over stdio by a small JSON-RPC client; nothing was written to the three repositories apart from a tokensave index in `marksy-api` (kept out of its git status through `.git/info/exclude`).

**Baseline:** 612/612 before the work. **Result:** 734/734 after. Seven real defects found and fixed by the sweep, each with a regression test; an independent review of the branch then found nine more issues in the new code, also fixed with tests.

## Defects found and fixed

1. **No repository tool worked over MCP.** Every tool that takes a repository hung for 120 seconds and then failed, on all three repositories. Child `git` processes inherited the server's stdin pipe and blocked. No existing test called a repository tool through the real server, so this had been present since Story S4. Fixed by giving every child process its own closed stdin (`LocalGitOperations`, `LocalGitChangeVerifier`, `ProcessVerificationRunner`). After the fix the same calls take about 0.5 seconds. Regression: `McpExecutable_ToolsCall_ForRepositoryTool_AgainstARealGitRepository_RespondsWithoutHanging`.
2. **MCP results carried no evidence.** A successful call returned only "Completed via WFL-DEL-0008." Fixed: results now append findings and results, or the full document for the grounding tools.
3. **`npm` could not be launched on Windows.** `npm`, `yarn`, `pnpm` and `mvn` are `.cmd` shims, which the process API cannot start directly, so build validation on any Node repository failed with "is it installed and on PATH?". Fixed by running batch shims through `cmd.exe`. Regression: `WindowsBatchShimOnPath_LikeNpm_IsRunnable`.
4. **Gradle Kotlin-DSL projects had no detected stack** (`marksy-os`). Fixed: `*.gradle.kts` is now a marker for a `gradle` stack whose command is the repository's own `gradlew test`.
5. **Repository-local build wrappers could not be run** (`gradlew.bat`, `mvnw.cmd`). Fixed: the working directory is searched before PATH.
6. **Dot-files named in a prompt were dropped** (`.gitignore`, `.github/...`). Fixed in the prompt extractor.
7. **The CLI's text output omitted the brief.** Fixed: text mode prints the evidence document.

## Existing tools (Story S8)

- `aef_health`, `aef_version`, both lifecycle starters: responded in under 30 ms.
- The three deferred tools returned the "recognized but deferred" message; an unknown tool name returned an error without crashing the server.
- `aef_analyze_repository`, `aef_plan_engineering`, `aef_plan_migration`, `aef_plan_maintenance`, `aef_manage_platform_lifecycle` on all three repositories: 15 calls, 0.4–0.5 seconds each, no unhandled errors, nothing on stdout that was not protocol.
- `aef_validate_repository`:
  - Ferret: `npm test` now launches and fails honestly with "'vitest' is not recognized" — the repository has no `node_modules` installed.
  - marksy-api: `python -m pytest` did not finish inside the 10-minute limit. Reported as such; the server did not hang.
  - marksy-os: before fix 4, "no known default build/test command".

## New tools

**`aef_refine_prompt`.** Fifteen prompts, five recent non-merge commit subjects per repository.

- Every call returned in 2.9–7.6 seconds with a brief of 2,000–3,300 characters (cap is 6,000).
- 209 cited file or `file:line` references were checked mechanically against the repositories: none pointed at a missing file, a line past the end of the file, or a line without the named symbol.
- Relevance is modest. A file that the real commit touched appeared in the candidate list for 10 of 15 prompts, and 16 of the 95 files the real commits touched were listed. Before the ranking change it was 7 of 15.
- Files found only by search were the right file 5 times in 30. As a result the brief now calls a file confirmed, and tells the agent to work in it, only when the prompt itself names it. Search matches are listed as "possible match, unconfirmed".
- Caveat: these indexes were at HEAD, so they already contained each commit's change. The with/without comparison in `AEF-Agent-Grounding-Comparison-2026-11.md` uses indexes built at the parent commit.
- Unavailable paths: with tokensave removed from PATH, and against the un-indexed `marksy-api`, a brief was still returned in under 1.1 seconds, stated why code intelligence was unavailable, and still verified files named in the prompt by checking the disk.

**`aef_check_change`.** For each of the same fifteen commits, called with the commit's parent as the base.

- The changed-file list equalled `git diff --name-only <parent>` in 15 of 15 cases, 2.8–4.1 seconds each.
- An unknown base ref and a clean tree were each reported plainly.

**CLI and REST.** The same refine request through `AefPlatform.Cli.dll` and `POST /api/v1/execution` returned the identical document. Both list 13 capabilities.

## LLM-backed skills through the local Claude

No API key was available, so `SKL-EXE-0026` to `0029` were run for the first time against a real model through the new `ClaudeCli` provider (`Ai:Provider = "ClaudeCli"`), using this repository's own grounding-layer design as the specification and the branch's commit log as the implementation record. Run in-process by a temporary test that was deleted afterwards.

- With `haiku`: three of four completed. `SKL-EXE-0026` returned JSON with an unclosed object; the platform reported "could not be parsed" and fabricated nothing.
- With `sonnet`: four of four completed, 38–63 seconds per call. The findings were supported by the inputs; for example, the validation correctly listed the parts of the specification that had not yet been done at that point. The default model is now `sonnet`.
- A default headless `claude -p` call loads the full Claude Code context and cost $0.77 for a one-word reply. The provider therefore runs slim: its own system prompt, no tools, no MCP servers, no saved session.

## Observations logged, not fixed

- The eight older MCP tools cannot be given a goal. Their fixed routing word becomes the request text, so `aef_analyze_repository` reports "No entry matching 'analyze' was found" and the planning tools return generic "requires human input" lines.
- An unreadable `RepositoryRoot` makes the older tools return "Completed" with findings saying it could not be read. The two new tools fail instead.
- A build or test run blocks the single MCP server for its whole duration, up to 10 minutes. `aef_check_change` accepts `SkipBuildAndTests` for that reason.
- The detected stack never becomes a verified fact in a brief, because the detector records a description of its basis, not a file name.
- The registered `aef-mcp` server pointed at `C:\POC\aef-platform`, a path that no longer exists. That, not a product defect, is why it failed to connect.
