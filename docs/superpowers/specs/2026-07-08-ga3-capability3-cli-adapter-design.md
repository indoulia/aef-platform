# GA-3 Capability 3: CLI Adapter — Design

**Status:** Independent Engineering Review complete (Changes required → revised). All four findings resolved: a new `AefPlatform.Composition` class library extracts the six dependency-light classes `Cli` needs, so `Cli` no longer references `AefPlatform.Host` (and its ASP.NET Core/Swashbuckle footprint) at all; the duplication-precedent citation is corrected to two real precedents with an honest risk caveat; the E2E test executable-location strategy is pinned down; the `FailFastOnRegistrationError` divergence from `Host` is now stated explicitly. Ready for the Implementation Plan. Cites `docs/superpowers/specs/2026-07-08-ga3-external-execution-channels-engineering-program.md` (GA-3), `docs/superpowers/specs/2026-07-08-ga3-capability1-runtime-host-design.md` (Capability 1), `docs/superpowers/specs/2026-07-08-ga3-capability2-rest-adapter-design.md` (Capability 2), and ADR-0025 rather than re-deriving them.

## Mission

GA-3 §4 Capability 3 (CLI Adapter): "deliberately after REST, since CLI is architecturally simpler (no host, no concurrency) and de-risking the shared `Runtime.Agent` contracts through REST first benefits CLI for free." This document designs a second, independent channel into `Runtime.Agent`: a short-lived CLI executable — not hosted inside `AefPlatform.Host`'s process, per GA-3 §3's own architecture ("a separate, short-lived executable... calls `Runtime.Agent` in-process, prints the `ExecutionResponse`, exits").

## Frozen Inputs (not redesigned by this document)

RC0, `Runtime.Ai`, `Runtime.Agent` (all contracts), `EngineeringExecutionPipeline`, ADR-0025, Capability 1 (Runtime Host), Capability 2 (REST Adapter). No file belonging to any of these is modified.

## Non-Goals

Chat, IDE, SDK, batch execution, streaming, authentication providers, identity providers, OAuth, JWT, rate limiting, caching, product-specific commands. No shared composition-root library extracted from `Host` (see Decision 2 — a deliberate scope boundary, not an oversight).

## Decision 1 — Project Structure

Three new/changed projects, mirroring Capability 2's `Adapters.Rest`/`Host` split, revised per Independent Engineering Review Finding 1 (below):

- **`src/Adapters.Cli`** (class library, `ProjectReference: Runtime.Agent` only, fitness-tested identically to `Adapters.Rest`): argument parsing into a plain DTO, response formatting (text and JSON), exit-code mapping. No execution logic, no ASP.NET Core dependency.
- **`src/Composition`** (`AefPlatform.Composition.csproj`, new class library, `ProjectReference: Runtime`, `Runtime.Agent` — `Kernel` flows transitively via `Runtime`, exactly as it already does for `Host` today): relocates six existing, dependency-light classes out of `src/Host` — `RuntimeArchitectureLoader`/`RuntimeArchitectureLoadReport`, `EngineeringArchitectureManifest`, `ArchitectureArtifact`, `PlatformHostOptions`, `AiProviderOptions`, `PermissiveActivationPolicyResolver`, `TrustedDefaultSecurityEvaluator`. Every one of these imports only `System.ComponentModel.DataAnnotations`, `Kernel.Governance`, `Kernel.Registry`, `Runtime.Bootstrapping`, `Microsoft.Extensions.Logging`, or `Runtime.Agent.Security` — none touch ASP.NET Core. **Their namespace stays `AefPlatform.Host`, unchanged**, despite the physical project move — a namespace need not match its assembly, and preserving it means zero existing file anywhere in `Host`, `Host.Tests`, or any other already-merged test needs a `using` statement changed; only `Host.csproj`'s `ProjectReference` list and the two files (`src/Host/Program.cs`, `src/Host/RuntimeStartupHostedService.cs`) that directly `using` these types are touched, and only to add the new project reference — a minimality choice, stated plainly rather than left to look like an oversight.
- **`src/Cli`** (`AefPlatform.Cli.csproj`, `OutputType: Exe`, a new composition root): references `Runtime`, `Runtime.Ai`, `Runtime.Agent`, `Adapters.Cli`, and **`AefPlatform.Composition`** — never `AefPlatform.Host`. `Cli` is a composition root exactly like `Host`, exempt from ADR-0025's "channel depends on `Runtime.Agent` only" rule for the same reason Capability 1 established for `Host` itself (a composition root sees every layer; ADR-0025 governs adapter/channel *logic*, not the executable assembling it) — but that exemption never extended to pulling in `Host`'s own unrelated ASP.NET Core/Swashbuckle footprint, which a `ProjectReference` to `AefPlatform.Host.csproj` would have transitively dragged in. Extracting the six shared classes into `AefPlatform.Composition` (a plain class library, no web SDK, no `FrameworkReference`) gives `Cli` everything it needs with zero HTTP-hosting weight — directly serving GA-3 §8's own requirement that CLI startup cost stay small.

**Fitness test:** `tests/Adapters.Cli.Tests/ArchitectureBoundaryTests.cs`, identical pattern to `Adapters.Rest.Tests`'s own — excludes `Runtime`/`Runtime.Ai`/`Kernel`, includes `Runtime.Agent`.

## Decision 2 — Accepted Duplication: No Shared Composition-Root *Wiring*

`Cli`'s `Program.cs` still duplicates the DI-wiring *sequence* (`AddSingleton<IAiProvider>`, the behaviors dictionary, `RuntimeBootstrapper.Boot`, `EngineeringExecutionPipeline`, `ICapabilityCatalog`, `ISessionStore`, `IAgentExecutionInterface`, `ISecurityEvaluator`) that `Host`'s `Program.cs` already contains — minus every REST-specific piece (no `WebApplication`, no Kestrel, no Swagger, no `RestAdapterOptions`) — even after Decision 1's extraction. This is real, acknowledged duplication, not an oversight, and is narrower in scope than it first appears: Decision 1 already removed the *classes* from the duplication surface; only the *sequence of DI registrations* remains duplicated.

**Why the sequence itself is not also extracted:** `Program.cs`'s top-level statements compile to an entry-point script, not a callable method — extracting the wiring itself into a shared "compose the whole graph" method is a larger, more architecturally significant move (a new abstraction GA-3's own Capability 3 chartering does not name as a deliverable) than relocating six already-standalone classes. This is the kind of "architectural change... scope expansion beyond the approved engineering program" this program's own execution discipline flags for a stop-and-ask, so it is deliberately declined here. Two real, already-merged precedents already accept this same category of duplication — `Host.Tests`'s `GA2AgentExecutionHostTests` and `GA3Capability1RuntimeHostTests` both duplicate the identical DI-wiring sequence in test code, and neither review flagged it as blocking. That said, test-fixture duplication is lower-risk than duplicating two *production* composition roots (a stale test fixture fails loudly; a stale production `Program.cs` might not) — this is a real, larger risk than the test precedent alone would suggest, acknowledged here rather than glossed over. A future hardening capability may extract the shared wiring itself once a third real (non-test) composition root exists, or if this duplication is found to have actually drifted.

**Mitigation:** `Cli`'s `Program.cs` includes a code comment pointing at `Host`'s own `Program.cs` by name, so a future change to one is more likely to prompt a check of the other — a documentation mitigation, not a structural one, honestly presented as such.

## Decision 3 — Commands

Two subcommands, manual argument parsing (no new NuGet dependency — `System.CommandLine` is still in prerelease/beta for the relevant version as of this program's timeframe, and two subcommands with a handful of flags each do not warrant a parsing library):

### `aef-cli execute --user-request <text> --project-profile-id <id> [--session-id <id>] [--payload-json <json>] [--output text|json]`

1. Parses flags into `CliExecuteArguments(string UserRequest, string ProjectProfileId, string? SessionId, string? PayloadJson, string Output)`.
2. Validates (`Adapters.Cli.CliExecuteArgumentsValidator`, mirroring `Adapters.Rest`'s validator): `--user-request`/`--project-profile-id` required and non-blank; `--payload-json`, if supplied, must parse as a JSON object (`Dictionary<string, object>`-shaped) — a parse failure is a usage error, not a runtime crash.
3. Builds a `Runtime.Agent.Security.SecurityContext` (`PrincipalId: Environment.UserName`, `Claims: null` — the CLI's own natural, already-available caller identity, unlike REST's HTTP-derived one) and calls `ISecurityEvaluator.Evaluate` before constructing an `ExecutionRequest` — same seam-before-execution invariant as REST (Decision 8 in Capability 2's design), on denial: prints the denial reason to stderr, exits without calling `Runtime.Agent`.
4. Builds `ExecutionRequest(Guid.NewGuid().ToString("N"), sessionId, userRequest, projectProfileId, payload)` and calls `IAgentExecutionInterface.Execute`.
5. Formats the `ExecutionResponse` per `--output` (`text`: a short human-readable summary — status, presented result, suggested next step, session id if any; `json`: the raw `ExecutionResponse` serialized, for scripting) to stdout.
6. Exits with the mapped code (Decision 4).

### `aef-cli capabilities [--output text|json]`

Calls `IAgentExecutionInterface.DiscoverCapabilities()`, formats identically (`text`: one line per capability; `json`: the raw array), exits `0`.

**Session persistence across invocations:** a `--session-id` flag only (no environment-variable fallback) — the human copies the `SessionId` echoed in one invocation's output into the next invocation's flag. Matches GA-3 §4's own phrasing ("a local convenience, e.g. an environment variable or a flag — a design-spec decision") — a flag is chosen over an environment variable because it is explicit at every call site (scriptable, greppable in shell history) rather than ambient, hidden state a script author could forget was set.

## Decision 4 — Exit Codes

| Condition | Exit Code | Rationale |
|---|---|---|
| `ExecutionStatus.Completed` | `0` | Success. |
| `ExecutionStatus.NeedsClarification` | `2` | A distinct, documented "more input needed" code — scripts can branch on it without parsing stdout. |
| `ExecutionStatus.Failed` | `1` | Ordinary failure — matches `Failed`'s own meaning; unlike REST (Capability 2 Decision 9), a CLI exit code has no equivalent to a `404`-shaped distinction, and inventing one here (e.g. a fourth code for unknown-session) would create an inconsistency with REST's own documented "most `Failed` outcomes are not distinguished beyond the body" position for no scripting benefit CLI callers have ever asked for — CLI callers already have the full `ExecutionResponse` body in `--output json` mode if they need the reason. |
| Security denial | `77` | `sysexits.h`'s `EX_NOPERM` ("permission denied") — a well-known BSD convention this codebase has no prior convention to contradict. |
| Argument validation failure | `64` | `sysexits.h`'s `EX_USAGE` ("command line usage error") — likewise a well-known, unambiguous convention. |
| Unhandled exception | `70` | `sysexits.h`'s `EX_SOFTWARE` ("internal software error") — never prints the exception's own message to stdout/stderr beyond a generic "An unexpected error occurred.", mirroring Capability 2 Decision 9's "do not expose internal exceptions." |

## Decision 5 — Startup Validation

`Cli`'s `Program.cs` calls `RuntimeArchitectureLoader.Load(runtimeHandle, EngineeringArchitectureManifest.KnownArtifacts)` synchronously, immediately after constructing the DI graph and before dispatching to a command handler — the same registry-integrity check `RuntimeStartupHostedService` performs for `Host`, reused as a direct method call rather than through `IHostedService` (which requires a running `IHost`; `Cli` has no long-running host, so there is nothing to attach a hosted service to). On failure, prints the failures to stderr and always exits `70` (`EX_SOFTWARE`) without executing the requested command — a corrupt/incomplete registry is a startup fault, not a per-command one.

**Deliberate divergence from `Host`:** `RuntimeStartupHostedService` honors `PlatformHostOptions.FailFastOnRegistrationError` (default `true`) to optionally continue past a registry failure with a logged error rather than aborting. `Cli` does not read this flag and always exits on a registry failure — a stated choice, not an omission: a REST/long-running host might reasonably keep serving other, unaffected endpoints while logging a startup defect, but a CLI invocation has exactly one job for its whole process lifetime, so "continue anyway" has no sensible meaning here — there is nothing left to do after the one command is skipped.

## Decision 6 — Logging

`Cli` does not configure `Microsoft.Extensions.Logging` at all — a short-lived process invoked once per command has no ongoing log stream to write to beyond what its own stdout/stderr output already communicates to the invoking script or human. This differs from `Host`'s convention (Capability 1 Decision 3) deliberately: `Host` is long-running and benefits from a log stream; `Cli`'s entire "log" is its one command's own printed result and exit code. No new logging abstraction either way.

## Testing Plan

- `ArchitectureBoundaryTests` (`Adapters.Cli.Tests`): excludes `Runtime`/`Runtime.Ai`/`Kernel`, includes `Runtime.Agent`.
- `CliExecuteArgumentsValidatorTests`: missing/blank `--user-request`/`--project-profile-id` rejected; malformed `--payload-json` rejected; a valid set of arguments passes.
- `CliOutputFormatterTests`: `ExecutionResponse` formats correctly in both `text` and `json` modes; `CapabilityDescriptor[]` likewise.
- `ExitCodeMapperTests`: each `ExecutionStatus` value and each failure category (security denial, validation failure) maps to its documented exit code (Decision 4's table, expressed as data-driven test cases).
- End-to-end process tests (`tests/Cli.Tests`, using `System.Diagnostics.Process` to actually launch the built `AefPlatform.Cli` executable — the only way to prove a real process boundary, exit code, and stdout/stderr behavior, mirroring how `Adapters.Rest.Tests` uses `WebApplicationFactory` for a real HTTP round-trip). **Locating the executable, pinned down to avoid CI flakiness:** `tests/Cli.Tests/Cli.Tests.csproj` takes a `ProjectReference` to `AefPlatform.Cli.csproj` (guaranteeing build ordering — the executable is always built before the test project that launches it), and the test fixture resolves the path via `AppContext.BaseDirectory` combined with a relative walk to `Cli`'s own output directory using the *same* build configuration the test itself is running under (`#if DEBUG`/`RELEASE`-free approach: read `System.Reflection.Assembly.GetExecutingAssembly().Location`'s directory and substitute `Cli.Tests` for `AefPlatform.Cli` in the path, since both projects share the same `bin/<Configuration>/net8.0/` layout convention already visible in every existing `bin/Debug/net8.0/*.dll` path this session's own build output uses) — invoked as `dotnet AefPlatform.Cli.dll <args>` (not the native apphost), which is portable across the Windows/Linux dev and CI environments this repository has run on so far without a platform-specific executable-extension check:
  - `execute` with valid arguments routing to a deterministic capability (the same `"feature"`-keyword path Capability 2's own tests use) → exit code `0`, stdout contains `"Completed"`.
  - `execute` with an unknown `--session-id` → exit code `1` (ordinary `Failed`, per Decision 4 — no special-cased code, unlike REST's 404).
  - `execute` with a blank `--user-request` → exit code `64`.
  - `capabilities` → exit code `0`, stdout lists 8 capabilities in text mode and a valid JSON array of 8 elements in `--output json` mode.
- Full existing suite (419 tests through Capability 2) stays green — re-run as the final gate.

## Documentation Updates (Definition of Done)

- Architecture doc: `Adapters.Cli`/`Cli` as the second, independent adapter, and the accepted-duplication decision (Decision 2), documented plainly rather than silently.
- CLI Guide: both commands, every flag, the exit-code table (Decision 4), the `--session-id` convention.

## Review

- ✓ `Runtime.Agent` remains the sole public execution boundary — `Adapters.Cli` never references `Runtime`/`Runtime.Ai`/`Kernel`, enforced by a fitness test.
- ✓ `Cli`'s composition-root exemption from ADR-0025 mirrors `Host`'s own already-accepted precedent, not a new exception — and does not additionally inherit `Host`'s own unrelated ASP.NET Core dependency footprint.
- ✓ Only two existing files (`src/Host/Program.cs`, `src/Host/RuntimeStartupHostedService.cs`) are touched, both only to add a new `using`/project reference to the extracted `AefPlatform.Composition` library — no behavior, contract, or namespace change anywhere in `Host`, `Host.Tests`, or any GA-1/GA-2/Capability-1/Capability-2 file.
- ✓ The accepted duplication (Decision 2) is disclosed, reasoned, and scoped, not silently introduced — and the alternative (a shared composition root) is named as an explicit future option, not foreclosed.
- ✓ No Non-Goal item appears anywhere in this design.
