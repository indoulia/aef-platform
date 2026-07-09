# Developer Guide

## Calling `IAgentExecutionInterface` in-process

`Runtime.Agent.IAgentExecutionInterface` (`AgentExecutionService`) is the sole, product-facing execution boundary (ADR-0025). A caller in the same process:

```csharp
var agent = serviceProvider.GetRequiredService<IAgentExecutionInterface>();
var response = agent.Execute(new ExecutionRequest(
    RequestId: Guid.NewGuid().ToString("N"),
    SessionId: null,
    UserRequest: "start a new feature",
    ProjectProfileId: "default-profile",
    Payload: null));
```

`response.Status` is one of `ExecutionStatus.Completed`, `NeedsClarification`, or `Failed`. `DiscoverCapabilities()` lists every dispatchable capability; `StartSession(projectProfileId)` begins a multi-turn session.

**Payload values from JSON:** if you're constructing a `Payload` dictionary from deserialized JSON (rather than literal C# values), pass it through `Runtime.Agent.PayloadNormalization.Normalize(...)` first — `System.Text.Json` boxes object-typed dictionary values as `JsonElement`, which the pipeline's own `is string`-style pattern matching never recognizes. Both `Adapters.Rest` and `Adapters.Cli` already do this; do the same for any new in-process caller building a payload from JSON.

## Running `AefPlatform.Host` locally

```bash
dotnet run --project src/Host
```

Listens on `http://localhost:5080` by default (`RestAdapter:Port`, `appsettings.json`). `GET /api/v1/health` confirms it booted. Works from any working directory — `ContentRootPath` is pinned to the executable's own directory, not the caller's current directory.

## Running `AefPlatform.Cli` locally

```bash
dotnet run --project src/Cli -- capabilities
dotnet run --project src/Cli -- execute --user-request "start a new feature" --project-profile-id default-profile
```

## Repository layout

- `src/Kernel`, `src/Runtime`, `src/Runtime.Ai`, `src/Runtime.Agent` — the frozen execution core (RC0 through GA-2).
- `src/Composition` — dependency-light classes shared by `Host` and `Cli` (Capability 3).
- `src/Adapters.Rest`, `src/Host` — the REST channel (Capability 2) and its composition root (Capability 1).
- `src/Adapters.Cli`, `src/Cli` — the CLI channel (Capability 3) and its own composition root.
