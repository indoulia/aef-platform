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

## Session persistence

Pass `--session-id` (the value echoed by a prior `execute` call's output) to continue a multi-turn conversation across separate invocations — no environment-variable fallback is supported.

## Security

Every `execute` call passes an `Environment.UserName`-derived `SecurityContext` through `ISecurityEvaluator` before `Runtime.Agent` is invoked — the trusted-default implementation authorizes every request.

## JSON output

`--output json` serializes with camelCase property names, matching the REST Adapter's own convention — a caller scripting against both channels sees the same casing either way.
