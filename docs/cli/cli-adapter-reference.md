# CLI Adapter Reference

Binary: `dotnet src/Cli/bin/<Configuration>/net8.0/AefPlatform.Cli.dll` (works from any working directory — the tool pins its own content root to its output directory, not the caller's current directory).

## `execute`

```
aef-cli execute --user-request <text> --project-profile-id <id> [--session-id <id>] [--payload-json <json>] [--output text|json]
```

Exit codes: `0` (`Completed`), `1` (`Failed`), `2` (`NeedsClarification`), `64` (usage error — missing/blank required flags or malformed `--payload-json`), `77` (security seam denied — never reached with the trusted-default evaluator), `70` (unexpected internal error, or a startup registry-validation failure).

## `capabilities`

```
aef-cli capabilities [--output text|json]
```

Always exits `0`.

## `install`

```
aef-cli install --version <version> --target-path <path>
```

Materializes AEF Platform by copying its own already-built output directory (`AppContext.BaseDirectory`) into `<path>`. Introduced to satisfy AEF Distribution's Component Installation Contract (`ai-engineering-framework`, Architecture Specification Addendum 1, §4.1/`INV-12`) — version-pinned, target-path-parameterized, idempotent, failure reported as an exit code, materialized location printed on success. This is AEF Platform's own capability; it has no dependency on, and no awareness of, `aef-distribution` beyond satisfying that published contract.

`--version` must exactly equal this build's own version (`Directory.Build.props`'s `<Version>`, currently `1.0.0`) — there is no artifact repository yet, so "installing a version" means "copying the build that is actually running," and a mismatched request fails rather than silently materializing the wrong build.

Exit codes: `0` (success — installed path printed to stdout), `64` (usage error — missing/blank `--version`/`--target-path`, or a version that does not match this build), `70` (materialization failed — source directory missing, or an I/O error copying to `--target-path`).

Idempotent: running twice with the same `--version`/`--target-path` leaves the same end state (files are overwritten in place, not duplicated or appended).

## Session persistence — not currently functional standalone

`execute` accepts `--session-id`, but the CLI has no `sessions` command and never calls `IAgentExecutionInterface.StartSession` — no CLI invocation ever produces a session id (`sessionId` is `null` in every `execute` response, in every status, including `NeedsClarification`). Each `execute` invocation is also its own process with its own empty `InMemorySessionStore`, entirely separate from the REST Adapter's own long-lived one — a session id created via `POST /api/v1/sessions` is never valid from the CLI, and vice versa.

In practice, passing any `--session-id` value to `execute` today always fails with `Unknown session.` (exit `1`) — this is the current, tested behavior (`ExecuteCommandTests.Execute_UnknownSessionId_ExitsOne`), not a bug. `--session-id` is reserved for a future session-creation command and/or a persistent, cross-process session store; neither exists yet.

## Security

Every `execute` call passes an `Environment.UserName`-derived `SecurityContext` through `ISecurityEvaluator` before `Runtime.Agent` is invoked — the trusted-default implementation authorizes every request.

## JSON output

`--output json` serializes with camelCase property names, matching the REST Adapter's own convention — a caller scripting against both channels sees the same casing either way.
