# MCP Adapter Reference

`AefPlatform.Mcp` is a stdio MCP server over the same execution core as the REST and CLI channels. It exposes 15 tools. It reads repositories; it never edits, commits to, or indexes one.

## Registering the server

Publish a copy first. Do not register the `bin/Debug` output: a running server locks those files and blocks the next build.

```
dotnet publish src/Mcp -c Release -o <install-dir>
claude mcp add aef-mcp -s user -- dotnet <install-dir>/AefPlatform.Mcp.dll
```

For Codex, add the same command (`dotnet`, with the dll path as its argument) as an MCP server in its configuration.

## Grounding tools

These two are the reason to connect a coding agent.

### `aef_refine_prompt`

Turns a user prompt into a grounded brief. Arguments, all required strings: `UserPrompt`, `RepositoryName`, `RepositoryRoot`.

The brief has eight sections:

- **Goal** — the request restated.
- **Verified facts** — each ends with the file, and line where there is one, that proves it. A file or symbol the prompt names is checked; if it exists it appears here.
- **Where to work** — files and symbols, each with `file:line`. A file the prompt names is confirmed. Everything found by search is marked `(possible match, unconfirmed)`.
- **How to build and test** — the conventional command for the detected stack.
- **Assumptions** — anything inferred, including the build command.
- **Open questions** — for example a file or symbol the prompt names that was not found, with the closest real matches. The agent should ask the user these before editing.
- **Acceptance checks** — checkable conditions; `aef_check_change` understands them.
- **Refined prompt** — a paste-ready prompt built from the above.

Example, abridged:

```
## Verified facts
- `.gitignore` exists (.gitignore)

## Open questions
- The prompt mentions `NoSuchThingAnywhere`, which was not found in the repository. Which file or symbol did you mean?

## Refined prompt
tidy .gitignore and fix NoSuchThingAnywhere
Work in: .gitignore.
```

The brief is assembled without an LLM and is capped at 6,000 characters.

**Prerequisite:** symbol lookup uses the `tokensave` command-line tool, which must be on PATH. Run `tokensave init` once in the target repository. Without tokensave, or without an index, a brief is still returned: it says code intelligence is unavailable and still checks named files on disk.

### `aef_check_change`

Reports on a change the agent has made. Required strings: `RepositoryName`, `RepositoryRoot`. Optional strings:

- `BaseRef` — what to compare against. Default: the merge-base of `HEAD` with the default branch; if none can be found, `HEAD` (uncommitted changes only), and the report says so.
- `AcceptanceChecks` — newline-separated, copied from the brief.
- `ExpectedFiles` — newline-separated, the files the change was meant to stay within.
- `SkipBuildAndTests` — `"true"` to skip running the build and tests. A test run blocks the server until it finishes, up to 10 minutes.

The report lists files changed (committed since the base, staged, unstaged and untracked), files outside `ExpectedFiles`, affected tests, the build and test result, and each acceptance check as `met`, `not met` or `cannot tell`. A check the tool cannot evaluate mechanically is `cannot tell`, never assumed met. The call is an error only when the build or tests ran and failed, or git could not be read.

## Other tools

| Tool | Arguments | Notes |
|---|---|---|
| `aef_health`, `aef_version` | none | `aef_health` reports `Healthy` unless a registered check fails; none is registered for this channel. |
| `aef_start_feature_lifecycle`, `aef_start_epic_lifecycle` | none | Start an orchestrated workflow. |
| `aef_analyze_repository`, `aef_plan_engineering`, `aef_plan_migration`, `aef_plan_maintenance`, `aef_manage_platform_lifecycle` | `RepositoryName`, `RepositoryRoot` | Deterministic. They cannot be given a goal over MCP today, so their findings are generic. |
| `aef_validate_repository` | `RepositoryName`, `RepositoryRoot` | Runs the detected build/test command. |
| `aef_deliver_implementation`, `aef_review_specification`, `aef_validate_implementation` | — | Listed but deferred: they need typed payloads no JSON channel can build. Calling one returns a clear "deferred" error. |

Every result carries its evidence as text after the first line.

## Configuration

`appsettings.json` next to the dll. `Ai:Provider` may be `OpenAI`, `AzureOpenAI` or `ClaudeCli`. `ClaudeCli` runs LLM-backed skills through the locally installed, signed-in `claude` CLI with no API key; `Ai:ClaudeCli:Model` defaults to `sonnet`. The grounding tools do not use an AI provider.

## Evidence

`docs/reliability/AEF-v1.0-MCP-Dogfooding-Sweep-2026-11.md` and `docs/reliability/AEF-Agent-Grounding-Comparison-2026-11.md`.
