# P1 — Runtime Foundation & Platform Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `aef-platform` bootable as a real, long-running process that loads its known engineering architecture into memory, validates registry integrity at startup, and fails fast on invalid state — closing the *actual* gaps between the existing Kernel/Runtime library and "AEF v1.0 RC0 — Prompt P1", without re-implementing anything that already works.

**Architecture:** Add one new executable project, `src/Host/AefPlatform.Host`, that wraps the existing `RuntimeBootstrapper.Boot()` composition root in a .NET Generic Host (`Microsoft.Extensions.Hosting`) for configuration loading, logging, and graceful shutdown. A new `RuntimeArchitectureLoader` registers a canonical `EngineeringArchitectureManifest` — the union of every governed artifact Id already proven individually registrable by this repo's own per-Epic dogfooding scenario tests — against the booted `RuntimeHandle.Kernel.Registry`, and reports success/failure. A `RuntimeStartupHostedService` runs the loader during Generic Host startup and throws (aborting boot) when `PlatformHostOptions.FailFastOnRegistrationError` is true and any artifact fails to register. Two small, additive extensions to the existing `IRegistry`/`RuntimeCatalog` (an `All()` enumeration) close the "architecture artifacts are discoverable" gap. Nothing in `Kernel.Bootstrapping`, `Runtime.Bootstrapping`, or any existing `DomainContent`/`Hosts`/`Orchestration` code is renamed, replaced, or redesigned.

**Tech Stack:** .NET 8 (matches existing `Kernel.csproj`/`Runtime.csproj`), `Microsoft.Extensions.Hosting` 8.0.0 (DI + Configuration + Logging + graceful shutdown come bundled), xUnit 2.5.3 / Microsoft.NET.Test.Sdk 17.8.0 (matches existing test projects).

## Global Constraints

- Reuse first, extend second, create new only when the capability genuinely does not exist (per direct user instruction). Do not rename or replace `KernelBootstrapper`, `RuntimeBootstrapper`, `IRegistry`, `InMemoryRegistry`, `RuntimeCatalog`, or any `DomainContent`/`Hosts`/`Orchestration` type.
- Do not introduce a second Registry implementation, a second bootstrapper, or a parallel "RC0 runtime" — there is exactly one runtime.
- Do not invent an inter-artifact dependency-declaration model — none exists in the current architecture (`DefinitionSubmission` carries only `Id`/`Domain`). Treat "required dependencies missing" at the granularity the architecture actually supports today (a Skill registered without a bound `IBehavior` is logged, not fabricated into a new blocking policy — see Task 7).
- `TargetFramework` for every new project is `net8.0`, `Nullable` and `ImplicitUsings` both `enable`, matching `Directory.Build.props` and every existing `.csproj`.
- New test project mirrors `tests/Runtime.Tests/Runtime.Tests.csproj` exactly for package versions: `coverlet.collector` 6.0.0, `Microsoft.NET.Test.Sdk` 17.8.0, `xunit` 2.5.3, `xunit.runner.visualstudio` 2.5.3.
- Every step's commit must leave `dotnet build` and `dotnet test` green for the whole solution (`AefPlatform.sln`).
- No placeholder implementations, no TODOs — every artifact in the manifest is a real Id already registered somewhere in this repo's own test suite (traceable per-entry in Task 4).

---

## Gap Analysis (P1 requirement → existing implementation → action)

| P1 Requirement | Existing Implementation | Gap | Action |
|---|---|---|---|
| Application bootstrap | `KernelBootstrapper.Boot()` / `RuntimeBootstrapper.Boot()` compose the full object graph deterministically | None | **Reuse** |
| Generic Host | Nothing — no executable project exists in `AefPlatform.sln` at all; the Kernel/Runtime are only ever invoked from xUnit tests | Real gap: nothing can run as a live process | **Implement** (`src/Host/AefPlatform.Host`, Task 3/6) |
| Dependency Injection | Manual composition-root pattern inside the Bootstrappers (deliberate, already tested) | Not a defect | **Reuse** the composition root; wrap its *output* (`RuntimeHandle`) as a DI singleton in the new Host only |
| Configuration loading / environment configuration | `KernelConfiguration`/`RuntimeConfiguration` are in-memory C# records passed as method args; no file or env-var loading anywhere | Real gap | **Implement** (`appsettings.json` + `PlatformHostOptions`, Task 5) |
| Service registration | Bootstrapper composition already wires every Kernel/Runtime service correctly | None | **Reuse** |
| Graceful shutdown | Nothing — no long-running process exists to shut down | Real gap | **Implement** via Generic Host's built-in `IHostApplicationLifetime` + Ctrl+C/SIGTERM handling (Task 7) |
| Agent/Skill/Contract/Workflow Registries | One generic `IRegistry`/`InMemoryRegistry`, string-`Domain`-tagged, with `Register`/`Query`, duplicate + malformed rejection already implemented and tested | Registration/Lookup/Duplicate-detection already satisfied. Enumeration ("what's registered") is missing | **Reuse** the registry; **extend** with `All()` (Task 1/2) — no new registry types |
| Runtime Loader (enumerate every registered artifact, validate registry integrity at startup) | Each Epic's real content (`SKL-EXE-*`/`AGT-EXE-*`/`WFL-DEL-*`/`ORC-EXE-*`) is registered only inside isolated `*ContentRegistrationTests`/`*DogfoodingScenarioTests` files — proven individually, never assembled or registered together as one production boot sequence | Real, genuine gap — this is the literal "load its engineering architecture into memory" gap | **Implement** `EngineeringArchitectureManifest` + `RuntimeArchitectureLoader` (Task 4/5) |
| Startup Validation (duplicate Ids / missing deps / invalid registrations / invalid config) | `InMemoryRegistry.Register` already returns `RejectedDuplicate`/`RejectedMalformed` instead of throwing or overwriting | Nothing currently treats a non-`Success` `RegisterResult` as a fatal *startup* failure; no configuration validation exists | **Implement**: `RuntimeStartupHostedService` fails fast on any non-`Success` result and on `PlatformHostOptions` validation failure (Task 5/6) |
| Logging | Only `IEvidenceSink` (a governance decision ledger, not general logging) — correct as-is for the Kernel/Runtime layer | Real gap only at the process/Host layer | **Implement**: `Microsoft.Extensions.Logging` inside the new Host project only; `IEvidenceSink` untouched |
| Exception handling | Kernel/Runtime already fail closed at each contract boundary via typed `Reject*` results, not exceptions (deliberate) | Gap only at the process boundary: an unhandled exception during startup should produce a clean failure, not a raw crash | **Implement**: `RuntimeStartupHostedService` throws a single, clearly-messaged `InvalidOperationException` that the Generic Host surfaces as a clean non-zero exit |
| Telemetry hooks | `IEvidenceSink` already records every Accept/Reject decision — the architecturally-correct extension point | None | **Reuse** |
| Automated tests (startup, DI, registry loading, validation, duplicate detection) | `Kernel.Tests`/`Runtime.Tests` already cover `Boot()`, duplicate/malformed rejection, Catalog projection at the library level | New Host project has no tests yet | **Implement**: `tests/Host.Tests` (Task 8) |

**Known, explicitly out-of-scope limitation** (do not silently fix in this plan — it is a pre-existing gap from Epic 0005, not a P1 defect): the Delivery Execution Skills/Agents (`SKL-EXE-0018`/`0019`/`0020`, `AGT-EXE-0010`–`0014`) were proven only at the `IBehavior`-invocation level (`Epic0005EndToEndDeliveryDogfoodingTests.cs`), never registered through `IRegistry`/`SkillHost`. Wiring them in would require adapting each Behavior's request-shaped constructor to `IBehavior.Execute(RuntimeExecutionContext)` — a real design decision belonging to a future prompt, not a P1 gap-close.

---

## File Structure

- `src/Kernel/Registry/IRegistry.cs` — **modify**: add `All()` enumeration member.
- `src/Kernel/Registry/InMemoryRegistry.cs` — **modify**: implement `All()`.
- `src/Runtime/Catalog/RuntimeCatalog.cs` — **modify**: add `All()` enumeration, projecting the same way `Query` does.
- `tests/Kernel.Tests/Registry/RegistryUnitTests.cs` — **modify**: add `All()` coverage.
- `tests/Runtime.Tests/Catalog/RuntimeCatalogUnitTests.cs` — **modify**: add `All()` coverage; update `FakeRegistry` for the new interface member.
- `src/Host/AefPlatform.Host.csproj` — **create**: new executable project.
- `src/Host/Program.cs` — **create**: Generic Host composition root.
- `src/Host/PlatformHostOptions.cs` — **create**: bound, validated configuration options.
- `src/Host/ArchitectureArtifact.cs` — **create**: `(Id, Domain)` record.
- `src/Host/EngineeringArchitectureManifest.cs` — **create**: the canonical, known-good artifact list.
- `src/Host/RuntimeArchitectureLoader.cs` — **create**: registers the manifest, reports integrity.
- `src/Host/RuntimeStartupHostedService.cs` — **create**: runs the loader at boot, fails fast, logs shutdown.
- `src/Host/appsettings.json` — **create**: default configuration.
- `tests/Host.Tests/Host.Tests.csproj` — **create**: new test project.
- `tests/Host.Tests/RuntimeArchitectureLoaderTests.cs` — **create**.
- `tests/Host.Tests/PlatformHostOptionsTests.cs` — **create**.
- `tests/Host.Tests/PlatformHostStartupTests.cs` — **create**: end-to-end Generic Host boot tests.
- `AefPlatform.sln` — **modify**: add both new projects (via `dotnet sln add`, not hand-edited).

---

## Task 1: Extend `IRegistry` with an `All()` enumeration

**Files:**
- Modify: `src/Kernel/Registry/IRegistry.cs`
- Modify: `src/Kernel/Registry/InMemoryRegistry.cs`
- Test: `tests/Kernel.Tests/Registry/RegistryUnitTests.cs`

**Interfaces:**
- Produces: `IRegistry.All(): IReadOnlyCollection<RegistryEntry>` — later tasks (`RuntimeCatalog.All()`, Task 2) depend on this exact signature.

- [ ] **Step 1: Write the failing test**

Append to `tests/Kernel.Tests/Registry/RegistryUnitTests.cs`, inside the `RegistryUnitTests` class (after `Register_Malformed_RecordsRejectDecisionOnEvidenceSink`, before the `FakeEvidenceSink` private class):

```csharp
    [Fact]
    public void All_NoEntries_ReturnsEmpty()
    {
        var registry = new InMemoryRegistry();

        Assert.Empty(registry.All());
    }

    [Fact]
    public void All_AfterRegistrations_ReturnsEveryEntry()
    {
        var registry = new InMemoryRegistry();
        registry.Register(new DefinitionSubmission("def-1", "domain-a"));
        registry.Register(new DefinitionSubmission("def-2", "domain-b"));
        registry.Register(new DefinitionSubmission("def-1", "domain-c")); // duplicate, rejected — must not appear twice

        var all = registry.All();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, e => e.Id == "def-1" && e.Domain == "domain-a");
        Assert.Contains(all, e => e.Id == "def-2" && e.Domain == "domain-b");
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Kernel.Tests/Kernel.Tests.csproj --filter "FullyQualifiedName~RegistryUnitTests.All_"`
Expected: build error — `'InMemoryRegistry' does not implement interface member 'IRegistry.All()'` (once Step 3 below adds the interface member) or, if done in the other order, a compile error that `All` does not exist on `InMemoryRegistry`. Confirm a RED result before proceeding.

- [ ] **Step 3: Add the interface member and implementation**

`src/Kernel/Registry/IRegistry.cs`:

```csharp
namespace Kernel.Registry;

// The Kernel-boundary shape of I4 §8's Registration Contract and Discovery
// read-contract. Public because Construction Units outside the Kernel
// (the future Instance Supervisor, S3; the future Catalog, out of this
// Epic's scope) call it to function — per I4 §7's public/internal rule.
public interface IRegistry
{
    RegisterResult Register(DefinitionSubmission submission);

    QueryResult Query(string id);

    IReadOnlyCollection<RegistryEntry> All();
}
```

`src/Kernel/Registry/InMemoryRegistry.cs` — add this member to the `InMemoryRegistry` class (anywhere after `Query`):

```csharp
    public IReadOnlyCollection<RegistryEntry> All() => _store.Values.ToList();
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Kernel.Tests/Kernel.Tests.csproj --filter "FullyQualifiedName~RegistryUnitTests"`
Expected: PASS, all tests in the file including the two new ones.

- [ ] **Step 5: Commit**

```bash
git add src/Kernel/Registry/IRegistry.cs src/Kernel/Registry/InMemoryRegistry.cs tests/Kernel.Tests/Registry/RegistryUnitTests.cs
git commit -m "feat(kernel): add IRegistry.All() enumeration for artifact discoverability"
```

---

## Task 2: Extend `RuntimeCatalog` with a matching `All()` projection

**Files:**
- Modify: `src/Runtime/Catalog/RuntimeCatalog.cs`
- Modify: `tests/Runtime.Tests/Catalog/RuntimeCatalogUnitTests.cs`

**Interfaces:**
- Consumes: `IRegistry.All(): IReadOnlyCollection<RegistryEntry>` (Task 1).
- Produces: `RuntimeCatalog.All(): IReadOnlyCollection<CatalogEntry>` — this is the "architecture artifacts are discoverable" surface `PlatformHostStartupTests` (Task 8) queries against.

- [ ] **Step 1: Write the failing test**

Append to `tests/Runtime.Tests/Catalog/RuntimeCatalogUnitTests.cs`, inside the `RuntimeCatalogUnitTests` class (after `Query_WithNoConformanceProviderConfigured_ContractConformanceResultIsNull`, before the `FakeRegistry` private class):

```csharp
    [Fact]
    public void All_NoEntries_ReturnsEmpty()
    {
        var registry = new InMemoryRegistry(new Kernel.Evidence.InMemoryEvidenceSink());
        var catalog = new RuntimeCatalog(registry);

        Assert.Empty(catalog.All());
    }

    [Fact]
    public void All_AfterRegistrations_ReturnsEveryEntryWithConformanceApplied()
    {
        var registry = new InMemoryRegistry(new Kernel.Evidence.InMemoryEvidenceSink());
        registry.Register(new DefinitionSubmission("skill-1", "domain-skills"));
        registry.Register(new DefinitionSubmission("strategy-1", "domain-strategies"));
        var catalog = new RuntimeCatalog(registry, new FixedConformanceProvider("strategy-1", "Conformant"));

        var all = catalog.All();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, e => e.Id == "skill-1" && e.ContractConformanceResult == null);
        Assert.Contains(all, e => e.Id == "strategy-1" && e.ContractConformanceResult == "Conformant");
    }
```

Then update the existing `FakeRegistry` private class (used by `Query_NeverOriginatesState_RegistryIsAuthoritative`) so the project still compiles against the wider `IRegistry` interface:

```csharp
    private sealed class FakeRegistry : IRegistry
    {
        public QueryResult NextResult { get; }

        public FakeRegistry(QueryResult nextResult) => NextResult = nextResult;

        public RegisterResult Register(DefinitionSubmission submission) => throw new NotSupportedException();

        public QueryResult Query(string id) => NextResult;

        public IReadOnlyCollection<RegistryEntry> All() => throw new NotSupportedException();
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Runtime.Tests/Runtime.Tests.csproj --filter "FullyQualifiedName~RuntimeCatalogUnitTests"`
Expected: compile error — `'RuntimeCatalog' does not contain a definition for 'All'` (and, before the `FakeRegistry` edit above, `'FakeRegistry' does not implement interface member 'IRegistry.All()'`).

- [ ] **Step 3: Implement `RuntimeCatalog.All()`**

Add to `src/Runtime/Catalog/RuntimeCatalog.cs`, inside the `RuntimeCatalog` class (after `Query`):

```csharp
    public IReadOnlyCollection<CatalogEntry> All()
    {
        return _registry.All()
            .Select(entry => new CatalogEntry(entry.Id, entry.Domain, entry.Status, _conformanceProvider?.GetConformanceResult(entry.Id)))
            .ToList();
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Runtime.Tests/Runtime.Tests.csproj --filter "FullyQualifiedName~RuntimeCatalogUnitTests"`
Expected: PASS, all tests in the file including the two new ones.

- [ ] **Step 5: Run the full existing suite to confirm no regression**

Run: `dotnet test AefPlatform.sln`
Expected: PASS, same total count as before Task 1 plus the four new tests.

- [ ] **Step 6: Commit**

```bash
git add src/Runtime/Catalog/RuntimeCatalog.cs tests/Runtime.Tests/Catalog/RuntimeCatalogUnitTests.cs
git commit -m "feat(runtime): add RuntimeCatalog.All() projection for artifact discoverability"
```

---

## Task 3: Scaffold the `AefPlatform.Host` executable project

**Files:**
- Create: `src/Host/AefPlatform.Host.csproj`
- Create: `src/Host/Program.cs` (placeholder — replaced fully in Task 6)
- Modify: `AefPlatform.sln`

**Interfaces:**
- Produces: a buildable, runnable (but not yet functional) console project at namespace `AefPlatform.Host`, referencing `Runtime.csproj`. Task 4-7 add real behavior into this project.

- [ ] **Step 1: Create the project via the .NET CLI**

Run (from the repo root, `/c/POC/aef-platform`):

```bash
dotnet new console -n AefPlatform.Host -o src/Host --framework net8.0
```

This generates `src/Host/AefPlatform.Host.csproj` and a default `src/Host/Program.cs`. Delete the generated `Program.cs` content and the default `<ItemGroup>`-less scaffolding is fine as-is.

- [ ] **Step 2: Edit the generated `.csproj` to match repo conventions and add the Hosting package**

Replace the full contents of `src/Host/AefPlatform.Host.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AefPlatform.Host</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Runtime\Runtime.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Replace `src/Host/Program.cs` with a minimal placeholder**

```csharp
var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();

await host.RunAsync();
```

- [ ] **Step 4: Add a minimal `appsettings.json` placeholder**

`src/Host/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

- [ ] **Step 5: Add the project to the solution**

Run:

```bash
dotnet sln AefPlatform.sln add src/Host/AefPlatform.Host.csproj
```

- [ ] **Step 6: Verify it builds and runs**

Run: `dotnet build AefPlatform.sln`
Expected: Build succeeded, 0 errors.

Run: `dotnet run --project src/Host/AefPlatform.Host.csproj &` then check it started without error, then stop it (Ctrl+C). Expected: process starts, logs the default Generic Host startup banner, and exits cleanly on Ctrl+C without an unhandled exception.

- [ ] **Step 7: Commit**

```bash
git add src/Host/AefPlatform.Host.csproj src/Host/Program.cs src/Host/appsettings.json AefPlatform.sln
git commit -m "feat(host): scaffold AefPlatform.Host executable project"
```

---

## Task 4: `EngineeringArchitectureManifest` — the canonical known-artifact list

**Files:**
- Create: `src/Host/ArchitectureArtifact.cs`
- Create: `src/Host/EngineeringArchitectureManifest.cs`
- Create: `tests/Host.Tests/Host.Tests.csproj`
- Test: `tests/Host.Tests/RuntimeArchitectureLoaderTests.cs` (manifest-shape assertions only in this task; loader behavior in Task 5)

**Interfaces:**
- Produces: `ArchitectureArtifact(string Id, string Domain)`; `EngineeringArchitectureManifest.KnownArtifacts: IReadOnlyList<ArchitectureArtifact>`. Task 5's `RuntimeArchitectureLoader.Load` consumes both exactly as named here.

- [ ] **Step 1: Scaffold the test project**

Run:

```bash
dotnet new xunit -n Host.Tests -o tests/Host.Tests --framework net8.0
```

Replace the generated `tests/Host.Tests/Host.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Host\AefPlatform.Host.csproj" />
    <ProjectReference Include="..\..\src\Runtime\Runtime.csproj" />
    <ProjectReference Include="..\..\src\Kernel\Kernel.csproj" />
  </ItemGroup>

</Project>
```

Delete the generated placeholder test file (`tests/Host.Tests/UnitTest1.cs`) — it is replaced by the files below.

Add to the solution:

```bash
dotnet sln AefPlatform.sln add tests/Host.Tests/Host.Tests.csproj
```

- [ ] **Step 2: Write the failing test**

Create `tests/Host.Tests/RuntimeArchitectureLoaderTests.cs`:

```csharp
using AefPlatform.Host;

namespace Host.Tests;

public class EngineeringArchitectureManifestTests
{
    [Fact]
    public void KnownArtifacts_ContainsNoDuplicateIds()
    {
        var ids = EngineeringArchitectureManifest.KnownArtifacts.Select(a => a.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void KnownArtifacts_EveryEntryHasNonEmptyIdAndDomain()
    {
        Assert.All(EngineeringArchitectureManifest.KnownArtifacts, artifact =>
        {
            Assert.False(string.IsNullOrWhiteSpace(artifact.Id));
            Assert.False(string.IsNullOrWhiteSpace(artifact.Domain));
        });
    }

    [Fact]
    public void KnownArtifacts_IncludesEveryEpicAlreadyProvenRegistrableInThisRepo()
    {
        var ids = EngineeringArchitectureManifest.KnownArtifacts.Select(a => a.Id).ToHashSet();

        // D9FeatureLifecycleContentRegistrationTests / FeatureLifecycleDogfoodingScenarioTests
        Assert.Contains("SKL-EXE-0004", ids);
        Assert.Contains("SKL-EXE-0005", ids);
        Assert.Contains("AGT-EXE-0002", ids);
        Assert.Contains("ORC-EXE-0001", ids);
        Assert.Contains("WFL-DEL-0001", ids);
        // NextEligibleItemDiscoveryRegistrationTests
        Assert.Contains("SKL-EXE-0012", ids);
        Assert.Contains("AGT-EXE-0005", ids);
        // EpicLifecycleContentRegistrationTests
        Assert.Contains("SKL-EXE-0013", ids);
        Assert.Contains("SKL-EXE-0014", ids);
        Assert.Contains("AGT-EXE-0006", ids);
        Assert.Contains("WFL-DEL-0004", ids);
        // RepositoryAssessmentContentRegistrationTests
        Assert.Contains("SKL-EXE-0015", ids);
        Assert.Contains("AGT-EXE-0007", ids);
        // ProjectRegistrationContentRegistrationTests
        Assert.Contains("SKL-EXE-0016", ids);
        Assert.Contains("AGT-EXE-0008", ids);
        // BaselineQualityGateContentRegistrationTests
        Assert.Contains("SKL-EXE-0017", ids);
        Assert.Contains("AGT-EXE-0009", ids);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj`
Expected: compile error — `The type or namespace name 'EngineeringArchitectureManifest' could not be found`.

- [ ] **Step 4: Implement `ArchitectureArtifact` and `EngineeringArchitectureManifest`**

`src/Host/ArchitectureArtifact.cs`:

```csharp
namespace AefPlatform.Host;

public sealed record ArchitectureArtifact(string Id, string Domain);
```

`src/Host/EngineeringArchitectureManifest.cs`:

```csharp
namespace AefPlatform.Host;

// Every governed artifact already proven independently registrable by this
// repository's own per-Epic dogfooding scenarios (ai-engineering-framework
// repo Epics 0001-0006) — see docs/superpowers/plans/2026-07-08-p1-runtime-
// foundation-platform-bootstrap.md's Gap Analysis for how this list was
// derived. Consolidates them into the one canonical set the Runtime Loader
// registers together at boot, instead of each Epic proving registrability
// only in an isolated, siloed test.
public static class EngineeringArchitectureManifest
{
    public static IReadOnlyList<ArchitectureArtifact> KnownArtifacts { get; } = new[]
    {
        new ArchitectureArtifact("SKL-EXE-0004", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0005", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0002", "Execution"),
        new ArchitectureArtifact("ORC-EXE-0001", "Execution"),
        new ArchitectureArtifact("WFL-DEL-0001", "Delivery"),
        new ArchitectureArtifact("SKL-EXE-0012", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0005", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0013", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0014", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0006", "Execution"),
        new ArchitectureArtifact("WFL-DEL-0004", "Delivery"),
        new ArchitectureArtifact("SKL-EXE-0015", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0007", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0016", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0008", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0017", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0009", "Execution"),
    };
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj`
Expected: PASS, all 3 tests.

- [ ] **Step 6: Commit**

```bash
git add src/Host/ArchitectureArtifact.cs src/Host/EngineeringArchitectureManifest.cs tests/Host.Tests AefPlatform.sln
git commit -m "feat(host): add canonical EngineeringArchitectureManifest"
```

---

## Task 5: `RuntimeArchitectureLoader` — register the manifest, report integrity

**Files:**
- Create: `src/Host/RuntimeArchitectureLoader.cs`
- Test: `tests/Host.Tests/RuntimeArchitectureLoaderTests.cs` (add a new test class to the same file)

**Interfaces:**
- Consumes: `RuntimeHandle` (`Runtime.Bootstrapping`, existing — `RuntimeHandle.Kernel.Registry: IRegistry`), `ArchitectureArtifact`/`EngineeringArchitectureManifest.KnownArtifacts` (Task 4).
- Produces: `RuntimeArchitectureLoadReport(int Attempted, int Succeeded, IReadOnlyList<string> Failures)` with `bool IsValid => Failures.Count == 0`; `RuntimeArchitectureLoader.Load(RuntimeHandle runtime, IReadOnlyList<ArchitectureArtifact> artifacts): RuntimeArchitectureLoadReport`. Task 7's `RuntimeStartupHostedService` consumes this exact signature.

- [ ] **Step 1: Write the failing tests**

Append to `tests/Host.Tests/RuntimeArchitectureLoaderTests.cs` (new class, same file, after `EngineeringArchitectureManifestTests`):

```csharp
using Kernel.Registry;
using Runtime.Bootstrapping;

namespace Host.Tests;

public class RuntimeArchitectureLoaderTests
{
    [Fact]
    public void Load_AllArtifactsRegisterCleanly_ReportsFullSuccessAndIsValid()
    {
        var runtime = RuntimeBootstrapper.Boot();
        var artifacts = new[]
        {
            new ArchitectureArtifact("art-1", "Execution"),
            new ArchitectureArtifact("art-2", "Delivery"),
        };

        var report = RuntimeArchitectureLoader.Load(runtime, artifacts);

        Assert.True(report.IsValid);
        Assert.Equal(2, report.Attempted);
        Assert.Equal(2, report.Succeeded);
        Assert.Empty(report.Failures);
        Assert.True(runtime.Kernel.Registry.Query("art-1").Found);
        Assert.True(runtime.Kernel.Registry.Query("art-2").Found);
    }

    [Fact]
    public void Load_DuplicateIdWithinManifest_ReportsFailureAndRegistersOnlyTheFirst()
    {
        var runtime = RuntimeBootstrapper.Boot();
        var artifacts = new[]
        {
            new ArchitectureArtifact("art-1", "Execution"),
            new ArchitectureArtifact("art-1", "Delivery"),
        };

        var report = RuntimeArchitectureLoader.Load(runtime, artifacts);

        Assert.False(report.IsValid);
        Assert.Equal(2, report.Attempted);
        Assert.Equal(1, report.Succeeded);
        Assert.Single(report.Failures);
        Assert.Contains("art-1", report.Failures[0]);
        Assert.Equal("Execution", runtime.Kernel.Registry.Query("art-1").Entry!.Domain);
    }

    [Fact]
    public void Load_IdAlreadyRegisteredBeforeLoadRuns_ReportsFailureViaRejectedDuplicate()
    {
        var runtime = RuntimeBootstrapper.Boot();
        runtime.Kernel.Registry.Register(new DefinitionSubmission("art-1", "Execution"));
        var artifacts = new[] { new ArchitectureArtifact("art-1", "Execution") };

        var report = RuntimeArchitectureLoader.Load(runtime, artifacts);

        Assert.False(report.IsValid);
        Assert.Equal(1, report.Attempted);
        Assert.Equal(0, report.Succeeded);
        Assert.Single(report.Failures);
    }

    [Fact]
    public void Load_MalformedArtifact_ReportsFailure()
    {
        var runtime = RuntimeBootstrapper.Boot();
        var artifacts = new[] { new ArchitectureArtifact("", "Execution") };

        var report = RuntimeArchitectureLoader.Load(runtime, artifacts);

        Assert.False(report.IsValid);
        Assert.Single(report.Failures);
    }

    [Fact]
    public void Load_TheRealManifest_RegistersEveryKnownArtifactCleanly()
    {
        var runtime = RuntimeBootstrapper.Boot();

        var report = RuntimeArchitectureLoader.Load(runtime, EngineeringArchitectureManifest.KnownArtifacts);

        Assert.True(report.IsValid);
        Assert.Equal(EngineeringArchitectureManifest.KnownArtifacts.Count, report.Succeeded);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj --filter "FullyQualifiedName~RuntimeArchitectureLoaderTests"`
Expected: compile error — `The type or namespace name 'RuntimeArchitectureLoader' could not be found`.

- [ ] **Step 3: Implement `RuntimeArchitectureLoader`**

`src/Host/RuntimeArchitectureLoader.cs`:

```csharp
using Kernel.Registry;
using Runtime.Bootstrapping;

namespace AefPlatform.Host;

public sealed record RuntimeArchitectureLoadReport(int Attempted, int Succeeded, IReadOnlyList<string> Failures)
{
    public bool IsValid => Failures.Count == 0;
}

// The "load the engineering architecture into memory" step: registers a
// known-good artifact set against the already-booted Runtime's Registry
// (Kernel.Registry.Register, unchanged from Epic 0001) and reports which
// artifacts, if any, failed to register — never overwrites, never retries,
// never silently drops a failure.
public static class RuntimeArchitectureLoader
{
    public static RuntimeArchitectureLoadReport Load(RuntimeHandle runtime, IReadOnlyList<ArchitectureArtifact> artifacts)
    {
        var seenInManifest = new HashSet<string>();
        var failures = new List<string>();

        foreach (var artifact in artifacts)
        {
            if (!seenInManifest.Add(artifact.Id))
            {
                failures.Add($"{artifact.Id}: duplicate Id within the Engineering Architecture Manifest.");
                continue;
            }

            var result = runtime.Kernel.Registry.Register(new DefinitionSubmission(artifact.Id, artifact.Domain));
            if (result.Outcome != RegisterOutcome.Success)
            {
                failures.Add($"{artifact.Id}: {result.Reason}");
            }
        }

        return new RuntimeArchitectureLoadReport(artifacts.Count, artifacts.Count - failures.Count, failures);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj`
Expected: PASS, all 8 tests (3 from Task 4 + 5 new).

- [ ] **Step 5: Commit**

```bash
git add src/Host/RuntimeArchitectureLoader.cs tests/Host.Tests/RuntimeArchitectureLoaderTests.cs
git commit -m "feat(host): add RuntimeArchitectureLoader with registry-integrity reporting"
```

---

## Task 6: `PlatformHostOptions` — bound, validated configuration

**Files:**
- Create: `src/Host/PlatformHostOptions.cs`
- Modify: `src/Host/appsettings.json`
- Create: `tests/Host.Tests/PlatformHostOptionsTests.cs`
- Modify: `src/Host/AefPlatform.Host.csproj` (add `Microsoft.Extensions.Options.DataAnnotations`)

**Interfaces:**
- Produces: `PlatformHostOptions { string PlatformName; bool FailFastOnRegistrationError = true; }`. Task 7's `RuntimeStartupHostedService` and Task 8's Host boot both consume this exact shape.

- [ ] **Step 1: Add the DataAnnotations validation package**

Add to `src/Host/AefPlatform.Host.csproj`'s existing `<ItemGroup>` with `Microsoft.Extensions.Hosting`:

```xml
    <PackageReference Include="Microsoft.Extensions.Options.DataAnnotations" Version="8.0.0" />
```

- [ ] **Step 2: Write the failing test**

Create `tests/Host.Tests/PlatformHostOptionsTests.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using AefPlatform.Host;

namespace Host.Tests;

public class PlatformHostOptionsTests
{
    [Fact]
    public void Defaults_FailFastOnRegistrationError_IsTrue()
    {
        var options = new PlatformHostOptions { PlatformName = "AEF Platform Runtime" };

        Assert.True(options.FailFastOnRegistrationError);
    }

    [Fact]
    public void Validation_MissingPlatformName_FailsValidation()
    {
        var options = new PlatformHostOptions { PlatformName = "" };
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(PlatformHostOptions.PlatformName)));
    }

    [Fact]
    public void Validation_PlatformNameProvided_Passes()
    {
        var options = new PlatformHostOptions { PlatformName = "AEF Platform Runtime" };
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        Assert.True(isValid);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj --filter "FullyQualifiedName~PlatformHostOptionsTests"`
Expected: compile error — `The type or namespace name 'PlatformHostOptions' could not be found`.

- [ ] **Step 4: Implement `PlatformHostOptions`**

`src/Host/PlatformHostOptions.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace AefPlatform.Host;

public sealed class PlatformHostOptions
{
    [Required(AllowEmptyStrings = false)]
    public string PlatformName { get; set; } = string.Empty;

    public bool FailFastOnRegistrationError { get; set; } = true;
}
```

- [ ] **Step 5: Update `appsettings.json` with real default values**

Replace `src/Host/appsettings.json`:

```json
{
  "AefPlatform": {
    "PlatformName": "AEF Platform Runtime",
    "FailFastOnRegistrationError": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj`
Expected: PASS, all tests.

- [ ] **Step 7: Commit**

```bash
git add src/Host/AefPlatform.Host.csproj src/Host/PlatformHostOptions.cs src/Host/appsettings.json tests/Host.Tests/PlatformHostOptionsTests.cs
git commit -m "feat(host): add validated PlatformHostOptions bound from configuration"
```

---

## Task 7: `RuntimeStartupHostedService` + `Program.cs` wiring — real boot, startup validation, graceful shutdown

**Files:**
- Create: `src/Host/RuntimeStartupHostedService.cs`
- Modify: `src/Host/Program.cs`

**Interfaces:**
- Consumes: `RuntimeHandle` (DI singleton), `IOptions<PlatformHostOptions>` (Task 6), `RuntimeArchitectureLoader.Load` + `EngineeringArchitectureManifest.KnownArtifacts` (Task 4/5), `ILogger<RuntimeStartupHostedService>`, `IHostApplicationLifetime`.
- Produces: a fully wired `Program.cs` that Task 8's integration tests build and start directly.

- [ ] **Step 1: Implement `RuntimeStartupHostedService`**

`src/Host/RuntimeStartupHostedService.cs`:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Runtime.Bootstrapping;

namespace AefPlatform.Host;

// The Runtime Loader's live wiring point (I3 §10's boot sequence, extended
// with this Prompt's startup-validation requirement): runs once, during
// Generic Host startup, after RuntimeBootstrapper.Boot() has already
// produced a RuntimeHandle. Throwing here aborts IHost.StartAsync/RunAsync
// with a clear message — the Generic Host's own documented behavior for a
// hosted service that fails during StartAsync — rather than booting into a
// silently-incomplete Registry.
public sealed class RuntimeStartupHostedService : IHostedService
{
    private readonly RuntimeHandle _runtime;
    private readonly IOptions<PlatformHostOptions> _options;
    private readonly ILogger<RuntimeStartupHostedService> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public RuntimeStartupHostedService(
        RuntimeHandle runtime,
        IOptions<PlatformHostOptions> options,
        ILogger<RuntimeStartupHostedService> logger,
        IHostApplicationLifetime lifetime)
    {
        _runtime = runtime;
        _options = options;
        _logger = logger;
        _lifetime = lifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AEF Platform starting: {PlatformName}", _options.Value.PlatformName);

        var report = RuntimeArchitectureLoader.Load(_runtime, EngineeringArchitectureManifest.KnownArtifacts);

        _logger.LogInformation(
            "Runtime Architecture Loader: {Succeeded}/{Attempted} artifacts registered.",
            report.Succeeded,
            report.Attempted);

        if (!report.IsValid)
        {
            foreach (var failure in report.Failures)
            {
                _logger.LogError("Registry integrity failure: {Failure}", failure);
            }

            if (_options.Value.FailFastOnRegistrationError)
            {
                throw new InvalidOperationException(
                    $"Startup validation failed: {report.Failures.Count} registration failure(s). {string.Join("; ", report.Failures)}");
            }
        }

        _lifetime.ApplicationStopping.Register(() => _logger.LogInformation("AEF Platform shutting down."));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AEF Platform stopped.");
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Wire it all up in `Program.cs`**

Replace `src/Host/Program.cs`:

```csharp
using AefPlatform.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runtime.Bootstrapping;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<PlatformHostOptions>()
    .Bind(builder.Configuration.GetSection("AefPlatform"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(_ => RuntimeBootstrapper.Boot());
builder.Services.AddHostedService<RuntimeStartupHostedService>();

var host = builder.Build();

await host.RunAsync();
```

- [ ] **Step 3: Verify the solution builds**

Run: `dotnet build AefPlatform.sln`
Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 4: Manually verify a live boot**

Run: `dotnet run --project src/Host/AefPlatform.Host.csproj`
Expected console output includes, in order: the "AEF Platform starting: AEF Platform Runtime" line, the "Runtime Architecture Loader: 17/17 artifacts registered." line, and the standard Generic Host "Application started" banner — with no error-level log lines. Stop with Ctrl+C; expect "AEF Platform shutting down." then "AEF Platform stopped." to print before the process exits with code 0.

- [ ] **Step 5: Commit**

```bash
git add src/Host/RuntimeStartupHostedService.cs src/Host/Program.cs
git commit -m "feat(host): wire RuntimeStartupHostedService into the Generic Host boot sequence"
```

---

## Task 8: `Host.Tests` integration coverage — startup, DI, registry loading, validation, duplicate detection

**Files:**
- Create: `tests/Host.Tests/PlatformHostStartupTests.cs`

**Interfaces:**
- Consumes: `Host.CreateApplicationBuilder` (Generic Host test pattern — no custom test server needed, this is a console host, not ASP.NET Core), everything produced by Tasks 4-7.

- [ ] **Step 1: Write the failing tests**

Create `tests/Host.Tests/PlatformHostStartupTests.cs`:

```csharp
using AefPlatform.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runtime.Bootstrapping;

namespace Host.Tests;

public class PlatformHostStartupTests
{
    private static HostApplicationBuilder CreateBuilder(IDictionary<string, string?> configOverrides)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(configOverrides);

        builder.Services
            .AddOptions<PlatformHostOptions>()
            .Bind(builder.Configuration.GetSection("AefPlatform"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton(_ => RuntimeBootstrapper.Boot());
        builder.Services.AddHostedService<RuntimeStartupHostedService>();

        return builder;
    }

    [Fact]
    public async Task StartAsync_ValidConfiguration_BootsAndRegistersEveryKnownArtifact()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["AefPlatform:PlatformName"] = "AEF Platform Runtime",
        });
        using var host = builder.Build();

        await host.StartAsync();

        var runtime = host.Services.GetRequiredService<RuntimeHandle>();
        foreach (var artifact in EngineeringArchitectureManifest.KnownArtifacts)
        {
            Assert.True(runtime.Kernel.Registry.Query(artifact.Id).Found, $"{artifact.Id} was not registered at startup.");
        }

        await host.StopAsync();
    }

    [Fact]
    public async Task StartAsync_MissingPlatformName_FailsStartupBeforeAnyArtifactIsRegistered()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["AefPlatform:PlatformName"] = "",
        });
        using var host = builder.Build();

        await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync());
    }

    [Fact]
    public async Task StartAsync_FailFastDisabled_LogsButDoesNotThrow_WhenAnArtifactAlreadyExists()
    {
        // Arrange a pre-populated Registry (simulating a prior partial boot)
        // by resolving RuntimeHandle first and registering one manifest Id
        // ahead of the hosted service's own Load call — proving the
        // fail-fast toggle genuinely gates the throw, not just the log.
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["AefPlatform:PlatformName"] = "AEF Platform Runtime",
            ["AefPlatform:FailFastOnRegistrationError"] = "false",
        });
        using var host = builder.Build();
        var runtime = host.Services.GetRequiredService<RuntimeHandle>();
        var firstKnownId = EngineeringArchitectureManifest.KnownArtifacts[0].Id;
        runtime.Kernel.Registry.Register(new Kernel.Registry.DefinitionSubmission(firstKnownId, "PreExisting"));

        var exception = await Record.ExceptionAsync(() => host.StartAsync());

        Assert.Null(exception);
        await host.StopAsync();
    }

    [Fact]
    public async Task StartAsync_FailFastEnabled_ThrowsWhenAnArtifactAlreadyExists()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["AefPlatform:PlatformName"] = "AEF Platform Runtime",
            ["AefPlatform:FailFastOnRegistrationError"] = "true",
        });
        using var host = builder.Build();
        var runtime = host.Services.GetRequiredService<RuntimeHandle>();
        var firstKnownId = EngineeringArchitectureManifest.KnownArtifacts[0].Id;
        runtime.Kernel.Registry.Register(new Kernel.Registry.DefinitionSubmission(firstKnownId, "PreExisting"));

        var exception = await Record.ExceptionAsync(() => host.StartAsync());

        Assert.NotNull(exception);
        Assert.Contains(firstKnownId, exception!.ToString());
    }

    [Fact]
    public void RuntimeHandle_IsResolvableAsASingleton_ProvingDependencyInjectionIsOperational()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["AefPlatform:PlatformName"] = "AEF Platform Runtime",
        });
        using var host = builder.Build();

        var first = host.Services.GetRequiredService<RuntimeHandle>();
        var second = host.Services.GetRequiredService<RuntimeHandle>();

        Assert.Same(first, second);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Host.Tests/Host.Tests.csproj --filter "FullyQualifiedName~PlatformHostStartupTests"`
Expected: these should mostly already pass once Tasks 4-7 are in place (this task adds coverage, not new production code) — run first to confirm which specific assertion, if any, is unmet. If `RuntimeHandle` resolves to a *new instance* per `GetRequiredService` call in `RuntimeHandle_IsResolvableAsASingleton...`, that indicates `AddSingleton` was not used correctly in Task 7 Step 2 — treat that as a genuine RED signal to fix, not a plan error.

- [ ] **Step 3: Fix forward if anything is RED**

If any test fails, the fix belongs in the already-created Task 6/7 files (`PlatformHostOptions.cs`, `RuntimeStartupHostedService.cs`, `Program.cs`) — not in this test file. Re-run after each fix.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test AefPlatform.sln`
Expected: PASS — every test in `Kernel.Tests`, `Runtime.Tests`, and `Host.Tests` green, 0 failures.

- [ ] **Step 5: Commit**

```bash
git add tests/Host.Tests/PlatformHostStartupTests.cs
git commit -m "test(host): cover Generic Host startup, DI, registry loading, and fail-fast validation"
```

---

## Task 9: Final verification and documentation update

**Files:**
- Modify: `README.md` (repo root)

**Interfaces:** None — this task closes out the plan with evidence, not new code.

- [ ] **Step 1: Run the full solution build and test suite one more time**

Run: `dotnet build AefPlatform.sln`
Expected: Build succeeded, 0 errors, 0 warnings.

Run: `dotnet test AefPlatform.sln`
Expected: PASS — record the total test count for the P1 completion summary.

- [ ] **Step 2: Manually re-verify the live boot + graceful shutdown one more time end-to-end**

Run: `dotnet run --project src/Host/AefPlatform.Host.csproj`, confirm the same clean-boot output as Task 7 Step 4, then send SIGINT (Ctrl+C) and confirm the process exits with code 0 and both shutdown log lines appear.

- [ ] **Step 3: Update `README.md`'s `## Status` section**

Add a new entry at the top of the `## Status` section (immediately after the `# AEF Platform Repository` header block, before the existing `**Epic 0006...**` line) — do not edit or remove any existing status entries:

```markdown
**AEF v1.0 RC0, Prompt P1 (Runtime Foundation & Platform Bootstrap) complete, 2026-07-08.** Gap-analyzed against the already-complete Kernel/Runtime (Epics 0001-0006) rather than re-implemented from scratch — see `docs/superpowers/plans/2026-07-08-p1-runtime-foundation-platform-bootstrap.md` for the full Gap Analysis. `src/Host/AefPlatform.Host` is the new executable entry point: a .NET Generic Host wrapping the existing `RuntimeBootstrapper.Boot()` composition root with configuration loading (`appsettings.json`, validated `PlatformHostOptions`), logging, and graceful shutdown. `RuntimeArchitectureLoader` registers the `EngineeringArchitectureManifest` — the 17 governed artifacts already individually proven registrable across Epics 0001-0006's own dogfooding scenarios, now assembled and registered together at real process boot for the first time — against the booted Registry, and the Host fails startup fast (`PlatformHostOptions.FailFastOnRegistrationError`, default `true`) on any registration failure. `IRegistry`/`RuntimeCatalog` each gained an additive `All()` enumeration for artifact discoverability. Epic 0005's Delivery Execution content (`SKL-EXE-0018`-`0020`, `AGT-EXE-0010`-`0014`) remains unregistered in the live Registry — a pre-existing gap from Epic 0005, explicitly out of this Prompt's scope; a future prompt must decide how to adapt its request-shaped Behaviors to `IBehavior.Execute(RuntimeExecutionContext)` before it can be added to the manifest.
```

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: record P1 (Runtime Foundation & Platform Bootstrap) completion"
```

---

## Definition of Done Evidence Map

| P1 DoD Item | Evidence |
|---|---|
| Runtime boots successfully | Task 7 Step 4 / Task 9 Step 2 manual run; `PlatformHostStartupTests.StartAsync_ValidConfiguration_...` |
| Dependency Injection is operational | `PlatformHostStartupTests.RuntimeHandle_IsResolvableAsASingleton_...` |
| Configuration loads correctly | `PlatformHostOptionsTests`; `appsettings.json` bound via `builder.Configuration.GetSection("AefPlatform")` |
| Registries load correctly | `RuntimeArchitectureLoaderTests.Load_TheRealManifest_...`; `PlatformHostStartupTests.StartAsync_ValidConfiguration_BootsAndRegistersEveryKnownArtifact` |
| Architecture artifacts are discoverable | `RegistryUnitTests.All_*`, `RuntimeCatalogUnitTests.All_*` (Tasks 1-2) |
| Startup validation succeeds | `RuntimeStartupHostedService` fail-fast path; `PlatformHostStartupTests.StartAsync_FailFastEnabled_ThrowsWhenAnArtifactAlreadyExists` |
| Duplicate detection works | `RegistryUnitTests.Register_DuplicateId_...` (pre-existing); `RuntimeArchitectureLoaderTests.Load_DuplicateIdWithinManifest_...` / `Load_IdAlreadyRegisteredBeforeLoadRuns_...` (new) |
| Automated tests pass | Task 9 Step 1 `dotnet test AefPlatform.sln` full run |
| Working tree clean / all work committed | Task 9 Step 4, final `git status` |
