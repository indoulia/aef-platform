# GA-3 Capability 3: CLI Adapter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement GA-3 Capability 3 per `docs/superpowers/specs/2026-07-08-ga3-capability3-cli-adapter-design.md`: extract a dependency-light `AefPlatform.Composition` library out of `Host`, then build `Adapters.Cli` (Runtime.Agent-only) and a new `Cli` executable that together give AEF a second, independent execution channel.

**Architecture:** `AefPlatform.Composition` (new class library, `Runtime`+`Runtime.Agent` only) holds six classes relocated from `Host` with their namespace unchanged. `Adapters.Cli` (Runtime.Agent-only, fitness-tested) holds argument parsing, validation, formatting, and exit-code mapping. `Cli` (new Exe, a composition root like `Host`) references `Runtime`, `Runtime.Ai`, `Runtime.Agent`, `Adapters.Cli`, `AefPlatform.Composition` — never `AefPlatform.Host`.

**Tech Stack:** .NET 8, no new NuGet packages (manual argument parsing, `System.Diagnostics.Process` for E2E tests — both already in the BCL/test SDK).

## Global Constraints

- Target framework: `net8.0` everywhere.
- `Adapters.Cli.csproj` references `Runtime.Agent` only — enforced by a fitness test.
- `Cli.csproj` never references `AefPlatform.Host.csproj` (avoids inheriting `Microsoft.NET.Sdk.Web`/`Swashbuckle.AspNetCore`).
- The six relocated classes' namespace stays `AefPlatform.Host` — zero `using` statement changes anywhere outside `Host.csproj`'s own `ProjectReference` list.
- No existing GA-1/GA-2/Capability-1/Capability-2 contract, behavior, or test changes — Task 1's file move is namespace-preserving and behavior-preserving.
- No Non-Goal item appears anywhere (Chat/IDE/SDK/batch/streaming/auth-providers/OAuth/JWT/rate-limiting/caching/product-specific commands).
- The full existing test suite (419 tests through Capability 2) must stay green throughout — re-run after Task 1 (to confirm the extraction is behavior-preserving) and again as the final gate (Task 6).

---

### Task 1: Extract `AefPlatform.Composition`

**Files:**
- Create: `src/Composition/AefPlatform.Composition.csproj`
- Create: `src/Composition/ArchitectureArtifact.cs`, `RuntimeArchitectureLoader.cs`, `EngineeringArchitectureManifest.cs`, `PlatformHostOptions.cs`, `AiProviderOptions.cs`, `PermissiveActivationPolicyResolver.cs`, `TrustedDefaultSecurityEvaluator.cs`
- Delete: the same seven files from `src/Host/`
- Modify: `src/Host/AefPlatform.Host.csproj` (add one `ProjectReference`)

**Interfaces:**
- Produces: the same public types (`AefPlatform.Host.ArchitectureArtifact`, `RuntimeArchitectureLoader`, `RuntimeArchitectureLoadReport`, `EngineeringArchitectureManifest`, `PlatformHostOptions`, `AiProviderOptions`, `PermissiveActivationPolicyResolver`, `TrustedDefaultSecurityEvaluator`) — namespace unchanged (`AefPlatform.Host`), only their physical assembly changes. `Host`'s own `Program.cs` and `RuntimeStartupHostedService.cs` need **no code changes** — they already `using AefPlatform.Host;` or are themselves in that namespace.

- [ ] **Step 1: Create the new project**

Create `src/Composition/AefPlatform.Composition.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Runtime\Runtime.csproj" />
    <ProjectReference Include="..\Runtime.Agent\Runtime.Agent.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create the seven relocated files (namespace unchanged, each with a physical-location header comment)**

Create `src/Composition/ArchitectureArtifact.cs`:

```csharp
// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
namespace AefPlatform.Host;

public sealed record ArchitectureArtifact(string Id, string Domain);
```

Create `src/Composition/RuntimeArchitectureLoader.cs`:

```csharp
// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
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

Create `src/Composition/EngineeringArchitectureManifest.cs` with the exact contents of the current `src/Host/EngineeringArchitectureManifest.cs` (44 `ArchitectureArtifact` entries — copy verbatim, do not retype by hand), prefixed with the same header comment:

```csharp
// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
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
        new ArchitectureArtifact("AGT-EXE-0010", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0018", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0019", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0020", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0011", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0021", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0022", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0012", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0023", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0024", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0025", "Execution"),
        new ArchitectureArtifact("WFL-DEL-0006", "Delivery"),
        new ArchitectureArtifact("AGT-EXE-0013", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0026", "Execution"),
        new ArchitectureArtifact("WFL-DEL-0007", "Delivery"),
        new ArchitectureArtifact("AGT-EXE-0014", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0027", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0028", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0029", "Execution"),
        new ArchitectureArtifact("WFL-DEL-0005", "Delivery"),
        new ArchitectureArtifact("AGT-EXE-0015", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0030", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0031", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0032", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0033", "Execution"),
        new ArchitectureArtifact("WFL-DEL-0008", "Delivery"),
        new ArchitectureArtifact("REPO-VALIDATION", "Execution"),
    };
}
```

Create `src/Composition/PlatformHostOptions.cs`:

```csharp
// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
using System.ComponentModel.DataAnnotations;

namespace AefPlatform.Host;

public sealed class PlatformHostOptions
{
    [Required(AllowEmptyStrings = false)]
    public string PlatformName { get; set; } = string.Empty;

    public bool FailFastOnRegistrationError { get; set; } = true;
}
```

Create `src/Composition/AiProviderOptions.cs`:

```csharp
// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
using System.ComponentModel.DataAnnotations;

namespace AefPlatform.Host;

public sealed class AiProviderOptions
{
    [Required]
    [RegularExpression("OpenAI|AzureOpenAI")]
    public string Provider { get; set; } = "OpenAI";

    public int TimeoutSeconds { get; set; } = 60;

    public OpenAiSection? OpenAi { get; set; }

    public AzureOpenAiSection? AzureOpenAi { get; set; }

    public sealed class OpenAiSection
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-4o";
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    }

    public sealed class AzureOpenAiSection
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Deployment { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = "2024-06-01";
    }
}
```

Create `src/Composition/PermissiveActivationPolicyResolver.cs`:

```csharp
// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
using Kernel.Governance;

namespace AefPlatform.Host;

public sealed class PermissiveActivationPolicyResolver : IPolicyResolver
{
    public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
}
```

Create `src/Composition/TrustedDefaultSecurityEvaluator.cs`:

```csharp
// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
using Microsoft.Extensions.Logging;
using Runtime.Agent.Security;

namespace AefPlatform.Host;

public sealed class TrustedDefaultSecurityEvaluator : ISecurityEvaluator
{
    private readonly ILogger<TrustedDefaultSecurityEvaluator> _logger;

    public TrustedDefaultSecurityEvaluator(ILogger<TrustedDefaultSecurityEvaluator> logger)
    {
        _logger = logger;
        _logger.LogDebug("Trusted-default security evaluator active - all requests are authorized.");
    }

    public SecurityDecision Evaluate(SecurityContext context) => new(IsAllowed: true, DenialReason: null);
}
```

- [ ] **Step 3: Delete the seven original files from `src/Host/`**

```bash
git rm src/Host/ArchitectureArtifact.cs src/Host/RuntimeArchitectureLoader.cs src/Host/EngineeringArchitectureManifest.cs src/Host/PlatformHostOptions.cs src/Host/AiProviderOptions.cs src/Host/PermissiveActivationPolicyResolver.cs src/Host/TrustedDefaultSecurityEvaluator.cs
```

- [ ] **Step 4: Add the new `ProjectReference` to `Host.csproj`**

In `src/Host/AefPlatform.Host.csproj`, find:

```xml
  <ItemGroup>
    <ProjectReference Include="..\Runtime\Runtime.csproj" />
    <ProjectReference Include="..\Runtime.Agent\Runtime.Agent.csproj" />
    <ProjectReference Include="..\Adapters.Rest\Adapters.Rest.csproj" />
  </ItemGroup>
```

Replace with:

```xml
  <ItemGroup>
    <ProjectReference Include="..\Runtime\Runtime.csproj" />
    <ProjectReference Include="..\Runtime.Agent\Runtime.Agent.csproj" />
    <ProjectReference Include="..\Adapters.Rest\Adapters.Rest.csproj" />
    <ProjectReference Include="..\Composition\AefPlatform.Composition.csproj" />
  </ItemGroup>
```

- [ ] **Step 5: Add the new project to the solution and build**

Run: `dotnet sln AefPlatform.sln add src/Composition/AefPlatform.Composition.csproj`
Run: `dotnet build AefPlatform.sln`
Expected: Build succeeded, 0 warnings, 0 errors. `Program.cs` and `RuntimeStartupHostedService.cs` needed no edits — verify by confirming `git status` shows no changes to either file.

- [ ] **Step 6: Run the full existing suite to confirm the extraction is behavior-preserving**

Run: `dotnet test AefPlatform.sln`
Expected: all 419 pre-existing tests still pass, unchanged — this move touches no test file, no contract, no behavior.

- [ ] **Step 7: Commit**

```bash
git add src/Composition src/Host/AefPlatform.Host.csproj AefPlatform.sln
git commit -m "refactor(composition): extract AefPlatform.Composition from Host"
```

---

### Task 2: `Adapters.Cli` project — argument DTO and validator

**Files:**
- Create: `src/Adapters.Cli/Adapters.Cli.csproj`
- Create: `src/Adapters.Cli/CliExecuteArguments.cs`
- Create: `src/Adapters.Cli/CliExecuteArgumentsValidator.cs`
- Test: `tests/Adapters.Cli.Tests/Adapters.Cli.Tests.csproj` (create)
- Test: `tests/Adapters.Cli.Tests/ArchitectureBoundaryTests.cs` (create)
- Test: `tests/Adapters.Cli.Tests/CliExecuteArgumentsValidatorTests.cs` (create)

**Interfaces:**
- Produces: `Adapters.Cli.CliExecuteArguments(string UserRequest, string ProjectProfileId, string? SessionId, string? PayloadJson, string Output)`, `CliExecuteArgumentsValidator.Validate(CliExecuteArguments) : string?`.

- [ ] **Step 1: Create the project**

Create `src/Adapters.Cli/Adapters.Cli.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Runtime.Agent\Runtime.Agent.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create the argument DTO**

Create `src/Adapters.Cli/CliExecuteArguments.cs`:

```csharp
namespace Adapters.Cli;

public sealed record CliExecuteArguments(string UserRequest, string ProjectProfileId, string? SessionId, string? PayloadJson, string Output);
```

- [ ] **Step 3: Create the test project and fitness test**

Create `tests/Adapters.Cli.Tests/Adapters.Cli.Tests.csproj`:

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
    <ProjectReference Include="..\..\src\Adapters.Cli\Adapters.Cli.csproj" />
    <ProjectReference Include="..\..\src\Runtime.Agent\Runtime.Agent.csproj" />
  </ItemGroup>

</Project>
```

Create `tests/Adapters.Cli.Tests/ArchitectureBoundaryTests.cs`:

```csharp
namespace Adapters.Cli.Tests;

public class ArchitectureBoundaryTests
{
    private static IReadOnlyList<string?> AdaptersCliReferencedAssemblyNames() =>
        typeof(CliExecuteArguments).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

    [Fact]
    public void AdaptersCliAssembly_DoesNotReferenceRuntime()
    {
        Assert.DoesNotContain("Runtime", AdaptersCliReferencedAssemblyNames());
    }

    [Fact]
    public void AdaptersCliAssembly_DoesNotReferenceRuntimeAi()
    {
        Assert.DoesNotContain("Runtime.Ai", AdaptersCliReferencedAssemblyNames());
    }

    [Fact]
    public void AdaptersCliAssembly_DoesNotReferenceKernel()
    {
        Assert.DoesNotContain("Kernel", AdaptersCliReferencedAssemblyNames());
    }

    [Fact]
    public void AdaptersCliAssembly_ReferencesRuntimeAgent()
    {
        Assert.Contains("Runtime.Agent", AdaptersCliReferencedAssemblyNames());
    }
}
```

- [ ] **Step 4: Write the failing validator tests**

Create `tests/Adapters.Cli.Tests/CliExecuteArgumentsValidatorTests.cs`:

```csharp
namespace Adapters.Cli.Tests;

public class CliExecuteArgumentsValidatorTests
{
    [Fact]
    public void Validate_ValidArguments_ReturnsNull()
    {
        var args = new CliExecuteArguments("start a new feature", "default-profile", null, null, "text");

        Assert.Null(CliExecuteArgumentsValidator.Validate(args));
    }

    [Fact]
    public void Validate_EmptyUserRequest_ReturnsError()
    {
        var args = new CliExecuteArguments("", "default-profile", null, null, "text");

        Assert.NotNull(CliExecuteArgumentsValidator.Validate(args));
    }

    [Fact]
    public void Validate_EmptyProjectProfileId_ReturnsError()
    {
        var args = new CliExecuteArguments("start a new feature", "", null, null, "text");

        Assert.NotNull(CliExecuteArgumentsValidator.Validate(args));
    }

    [Fact]
    public void Validate_MalformedPayloadJson_ReturnsError()
    {
        var args = new CliExecuteArguments("start a new feature", "default-profile", null, "{ not valid json", "text");

        Assert.NotNull(CliExecuteArgumentsValidator.Validate(args));
    }

    [Fact]
    public void Validate_ValidPayloadJson_ReturnsNull()
    {
        var args = new CliExecuteArguments("start a new feature", "default-profile", null, "{\"key\":\"value\"}", "text");

        Assert.Null(CliExecuteArgumentsValidator.Validate(args));
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail to compile**

Run: `dotnet test tests/Adapters.Cli.Tests/Adapters.Cli.Tests.csproj --filter "FullyQualifiedName~CliExecuteArgumentsValidatorTests"`
Expected: build FAILS — `CliExecuteArgumentsValidator` does not exist.

- [ ] **Step 6: Implement the validator**

Create `src/Adapters.Cli/CliExecuteArgumentsValidator.cs`:

```csharp
using System.Text.Json;

namespace Adapters.Cli;

public static class CliExecuteArgumentsValidator
{
    public static string? Validate(CliExecuteArguments arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments.UserRequest))
        {
            return "--user-request must not be empty.";
        }

        if (string.IsNullOrWhiteSpace(arguments.ProjectProfileId))
        {
            return "--project-profile-id must not be empty.";
        }

        if (arguments.PayloadJson is not null)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(arguments.PayloadJson);
                if (parsed is null)
                {
                    return "--payload-json must not be a JSON null.";
                }
            }
            catch (JsonException)
            {
                return "--payload-json must be valid JSON representing an object.";
            }
        }

        return null;
    }
}
```

- [ ] **Step 7: Run the solution build and the new tests**

Run: `dotnet sln AefPlatform.sln add src/Adapters.Cli/Adapters.Cli.csproj tests/Adapters.Cli.Tests/Adapters.Cli.Tests.csproj`
Run: `dotnet build AefPlatform.sln`
Expected: Build succeeded.

Run: `dotnet test tests/Adapters.Cli.Tests/Adapters.Cli.Tests.csproj`
Expected: `ArchitectureBoundaryTests` — 3 of 4 pass (`AdaptersCliAssembly_ReferencesRuntimeAgent` fails until Task 3 adds real `Runtime.Agent` usage, same sequencing as Capability 2 Task 1/3). `CliExecuteArgumentsValidatorTests` — all 5 PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Adapters.Cli tests/Adapters.Cli.Tests AefPlatform.sln
git commit -m "feat(adapters.cli): scaffold Adapters.Cli with the execute-arguments DTO and validator"
```

---

### Task 3: Response formatting and exit-code mapping

**Files:**
- Create: `src/Adapters.Cli/CliOutputFormatter.cs`
- Create: `src/Adapters.Cli/CliExitCodeMapper.cs`
- Test: `tests/Adapters.Cli.Tests/CliOutputFormatterTests.cs` (create)
- Test: `tests/Adapters.Cli.Tests/CliExitCodeMapperTests.cs` (create)

**Interfaces:**
- Consumes: `Runtime.Agent.ExecutionResponse`, `Runtime.Agent.ExecutionStatus`, `Runtime.Agent.Capabilities.CapabilityDescriptor`.
- Produces: `CliOutputFormatter.FormatExecutionResponse(ExecutionResponse, string output) : string`, `CliOutputFormatter.FormatCapabilities(IReadOnlyList<CapabilityDescriptor>, string output) : string`, `CliExitCodeMapper.MapExecutionStatus(ExecutionStatus) : int`, plus the fixed constants `CliExitCodeMapper.SecurityDenied = 77`, `CliExitCodeMapper.UsageError = 64`, `CliExitCodeMapper.SoftwareError = 70` — consumed by Task 4's `Cli` executable.

- [ ] **Step 1: Write the failing tests**

Create `tests/Adapters.Cli.Tests/CliExitCodeMapperTests.cs`:

```csharp
using Runtime.Agent;

namespace Adapters.Cli.Tests;

public class CliExitCodeMapperTests
{
    [Theory]
    [InlineData(ExecutionStatus.Completed, 0)]
    [InlineData(ExecutionStatus.NeedsClarification, 2)]
    [InlineData(ExecutionStatus.Failed, 1)]
    public void MapExecutionStatus_ReturnsDocumentedCode(ExecutionStatus status, int expectedExitCode)
    {
        Assert.Equal(expectedExitCode, CliExitCodeMapper.MapExecutionStatus(status));
    }

    [Fact]
    public void FixedExitCodes_MatchSysexits()
    {
        Assert.Equal(64, CliExitCodeMapper.UsageError);
        Assert.Equal(70, CliExitCodeMapper.SoftwareError);
        Assert.Equal(77, CliExitCodeMapper.SecurityDenied);
    }
}
```

Create `tests/Adapters.Cli.Tests/CliOutputFormatterTests.cs`:

```csharp
using Runtime.Agent;
using Runtime.Agent.Capabilities;

namespace Adapters.Cli.Tests;

public class CliOutputFormatterTests
{
    private static ExecutionResponse SampleResponse() => new(
        "req-1", "session-1", ExecutionStatus.Completed, null, "Done.", "No further action needed.",
        new ExecutionEvidence(Array.Empty<string>(), null, null, null, null),
        new ExecutionMetadata("WFL-DEL-0001", null, TimeSpan.FromSeconds(1), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

    [Fact]
    public void FormatExecutionResponse_TextMode_ContainsStatusAndPresentedResult()
    {
        var formatted = CliOutputFormatter.FormatExecutionResponse(SampleResponse(), "text");

        Assert.Contains("Completed", formatted);
        Assert.Contains("Done.", formatted);
    }

    [Fact]
    public void FormatExecutionResponse_JsonMode_IsValidJson()
    {
        var formatted = CliOutputFormatter.FormatExecutionResponse(SampleResponse(), "json");

        using var document = System.Text.Json.JsonDocument.Parse(formatted);
        Assert.True(document.RootElement.TryGetProperty("status", out _));
    }

    [Fact]
    public void FormatCapabilities_TextMode_ListsEachCapability()
    {
        var capabilities = new[] { new CapabilityDescriptor("WFL-DEL-0001", "Feature", "desc", Array.Empty<string>(), "Synchronous") };

        var formatted = CliOutputFormatter.FormatCapabilities(capabilities, "text");

        Assert.Contains("WFL-DEL-0001", formatted);
    }

    [Fact]
    public void FormatCapabilities_JsonMode_IsValidJsonArray()
    {
        var capabilities = new[] { new CapabilityDescriptor("WFL-DEL-0001", "Feature", "desc", Array.Empty<string>(), "Synchronous") };

        var formatted = CliOutputFormatter.FormatCapabilities(capabilities, "json");

        using var document = System.Text.Json.JsonDocument.Parse(formatted);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, document.RootElement.ValueKind);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test tests/Adapters.Cli.Tests/Adapters.Cli.Tests.csproj --filter "FullyQualifiedName~CliExitCodeMapperTests|FullyQualifiedName~CliOutputFormatterTests"`
Expected: build FAILS — `CliExitCodeMapper`/`CliOutputFormatter` do not exist.

- [ ] **Step 3: Implement both classes**

Create `src/Adapters.Cli/CliExitCodeMapper.cs`:

```csharp
using Runtime.Agent;

namespace Adapters.Cli;

public static class CliExitCodeMapper
{
    public const int UsageError = 64;
    public const int SoftwareError = 70;
    public const int SecurityDenied = 77;

    public static int MapExecutionStatus(ExecutionStatus status) => status switch
    {
        ExecutionStatus.Completed => 0,
        ExecutionStatus.NeedsClarification => 2,
        ExecutionStatus.Failed => 1,
        _ => SoftwareError,
    };
}
```

Create `src/Adapters.Cli/CliOutputFormatter.cs`:

```csharp
using System.Text.Json;
using Runtime.Agent;
using Runtime.Agent.Capabilities;

namespace Adapters.Cli;

public static class CliOutputFormatter
{
    public static string FormatExecutionResponse(ExecutionResponse response, string output)
    {
        if (output == "json")
        {
            return JsonSerializer.Serialize(response);
        }

        var lines = new List<string>
        {
            $"Status: {response.Status}",
        };

        if (response.SessionId is not null)
        {
            lines.Add($"SessionId: {response.SessionId}");
        }

        if (response.PresentedResult is not null)
        {
            lines.Add(response.PresentedResult);
        }

        if (response.SuggestedNextStep is not null)
        {
            lines.Add($"Next step: {response.SuggestedNextStep}");
        }

        if (response.Error is not null)
        {
            lines.Add($"Error: {response.Error.Reason}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatCapabilities(IReadOnlyList<CapabilityDescriptor> capabilities, string output)
    {
        if (output == "json")
        {
            return JsonSerializer.Serialize(capabilities);
        }

        return string.Join(Environment.NewLine, capabilities.Select(c => $"{c.CapabilityId} - {c.DisplayName}: {c.Description}"));
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Adapters.Cli.Tests/Adapters.Cli.Tests.csproj`
Expected: all tests PASS, including `ArchitectureBoundaryTests.AdaptersCliAssembly_ReferencesRuntimeAgent` (now genuinely exercised by these two new files' use of `Runtime.Agent` types).

- [ ] **Step 5: Commit**

```bash
git add src/Adapters.Cli/CliOutputFormatter.cs src/Adapters.Cli/CliExitCodeMapper.cs tests/Adapters.Cli.Tests/CliOutputFormatterTests.cs tests/Adapters.Cli.Tests/CliExitCodeMapperTests.cs
git commit -m "feat(adapters.cli): add response formatting and exit-code mapping"
```

---

### Task 4: The `Cli` executable

**Files:**
- Create: `src/Cli/AefPlatform.Cli.csproj`
- Create: `src/Cli/Program.cs`
- Create: `src/Cli/appsettings.json`

**Interfaces:**
- Consumes: everything from Tasks 1–3 plus `Runtime.Agent.IAgentExecutionInterface`, `Runtime.Agent.Security.ISecurityEvaluator`/`SecurityContext`.
- Produces: a working `dotnet src/Cli/bin/<Config>/net8.0/AefPlatform.Cli.dll execute|capabilities ...` command-line tool — consumed by Task 5's E2E tests.

- [ ] **Step 1: Create the project**

Create `src/Cli/AefPlatform.Cli.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AefPlatform.Cli</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Options.DataAnnotations" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Runtime\Runtime.csproj" />
    <ProjectReference Include="..\Runtime.Ai\Runtime.Ai.csproj" />
    <ProjectReference Include="..\Runtime.Agent\Runtime.Agent.csproj" />
    <ProjectReference Include="..\Adapters.Cli\Adapters.Cli.csproj" />
    <ProjectReference Include="..\Composition\AefPlatform.Composition.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create a minimal `appsettings.json`, matching `Host`'s own `Ai`/`AefPlatform` sections**

Create `src/Cli/appsettings.json`:

```json
{
  "AefPlatform": {
    "PlatformName": "AEF Platform CLI",
    "FailFastOnRegistrationError": true
  },
  "Ai": {
    "Provider": "OpenAI",
    "TimeoutSeconds": 60,
    "OpenAi": { "ApiKey": "", "Model": "gpt-4o", "BaseUrl": "https://api.openai.com/v1" },
    "AzureOpenAi": { "Endpoint": "", "ApiKey": "", "Deployment": "", "ApiVersion": "2024-06-01" }
  }
}
```

- [ ] **Step 3: Implement `Program.cs`**

Create `src/Cli/Program.cs`:

```csharp
// This composition root deliberately duplicates the DI-wiring sequence in
// src/Host/Program.cs (minus every REST-specific piece) rather than
// sharing it - see Capability 3's design Decision 2 for the accepted
// tradeoff. If you change one, check the other.
using Adapters.Cli;
using AefPlatform.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Runtime.Agent;
using Runtime.Agent.Capabilities;
using Runtime.Agent.Security;
using Runtime.Agent.Sessions;
using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Ai.Providers;
using Runtime.Bootstrapping;
using Runtime.DomainContent.ConversationManagement;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.DomainContent.EngineeringPlanning;
using Runtime.DomainContent.EngineeringSpecificationReview;
using Runtime.DomainContent.ImplementationValidation;
using Runtime.DomainContent.RepositoryIntelligence;
using Runtime.DomainContent.RepositoryValidation;
using Runtime.Execution;
using Runtime.Hosts.Skill;

// Host.CreateApplicationBuilder() is called WITHOUT the CLI's own `args`
// (unlike Host's own Program.cs, which legitimately has no positional
// arguments of its own to worry about) - passing this process's args
// straight through would feed them to the Generic Host's own
// command-line configuration provider, which would try to interpret
// this CLI's own domain flags (--user-request, --project-profile-id,
// ...) as configuration-key overrides. args is parsed separately, by
// this file's own ParseFlags, once command dispatch begins.
var builder = Host.CreateApplicationBuilder();

builder.Services
    .AddOptions<PlatformHostOptions>()
    .Bind(builder.Configuration.GetSection("AefPlatform"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<AiProviderOptions>()
    .Bind(builder.Configuration.GetSection("Ai"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(new HttpClient());

builder.Services.AddSingleton<IAiProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AiProviderOptions>>().Value;
    var httpClient = sp.GetRequiredService<HttpClient>();
    return options.Provider switch
    {
        "AzureOpenAI" => new AzureOpenAiProvider(httpClient, new AzureOpenAiProviderOptions
        {
            Endpoint = options.AzureOpenAi!.Endpoint,
            ApiKey = options.AzureOpenAi.ApiKey,
            Deployment = options.AzureOpenAi.Deployment,
            ApiVersion = options.AzureOpenAi.ApiVersion,
        }),
        _ => new OpenAiProvider(httpClient, new OpenAiProviderOptions
        {
            ApiKey = options.OpenAi!.ApiKey,
            Model = options.OpenAi.Model,
            BaseUrl = options.OpenAi.BaseUrl,
        }),
    };
});

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<AiProviderOptions>>().Value;
    return new AiExecutionService(sp.GetRequiredService<IAiProvider>(), timeout: TimeSpan.FromSeconds(options.TimeoutSeconds));
});

builder.Services.AddSingleton<IPromptCatalog, EngineeringPromptCatalog>();

builder.Services.AddSingleton(sp =>
{
    IReadOnlyDictionary<string, IBehavior> behaviors = new Dictionary<string, IBehavior>
    {
        ["SKL-EXE-0018"] = new DeliveryImplementationSkillBehavior(),
        ["SKL-EXE-0019"] = new DeliveryValidationSkillBehavior(),
        ["SKL-EXE-0020"] = new DeliveryDocumentationSkillBehavior(),
        ["SKL-EXE-0021"] = new ConversationIntakeAndRoutingBehavior(),
        ["SKL-EXE-0022"] = new ConversationHandoffAndFollowupBehavior(),
        ["SKL-EXE-0023"] = new EngineeringDiscoveryAndGoalClarificationBehavior(),
        ["SKL-EXE-0024"] = new SpecificationDraftingAndAlignmentBehavior(),
        ["SKL-EXE-0025"] = new ReadinessClassificationAndApprovalRequestBehavior(),
        ["SKL-EXE-0026"] = new SpecificationIndependentReviewBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
        ["SKL-EXE-0027"] = new SpecificationConformanceValidationBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
        ["SKL-EXE-0028"] = new ArchitectureStandardsConformanceReviewBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
        ["SKL-EXE-0029"] = new ValidationFindingsSynthesisBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
        ["SKL-EXE-0030"] = new RepositoryAndArchitectureUnderstandingBehavior(),
        ["SKL-EXE-0031"] = new CodeIntelligenceAndQualitySignalDetectionBehavior(),
        ["SKL-EXE-0032"] = new EngineeringDiagnosticsAndRootCauseAnalysisBehavior(),
        ["SKL-EXE-0033"] = new RecommendationSynthesisAndEvidenceBasedReportingBehavior(),
        ["REPO-VALIDATION"] = new RepositoryValidationSkillBehavior(),
    };

    return RuntimeBootstrapper.Boot(
        new RuntimeConfiguration(PolicyResolver: new PermissiveActivationPolicyResolver()),
        behaviors);
});
builder.Services.AddSingleton(sp => new EngineeringExecutionPipeline(sp.GetRequiredService<RuntimeHandle>()));
builder.Services.AddSingleton<ICapabilityCatalog, EngineeringAgentCapabilityCatalog>();
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddSingleton<IAgentExecutionInterface>(sp => new AgentExecutionService(
    sp.GetRequiredService<EngineeringExecutionPipeline>(),
    sp.GetRequiredService<ICapabilityCatalog>(),
    sp.GetRequiredService<ISessionStore>()));
builder.Services.AddSingleton<ISecurityEvaluator, TrustedDefaultSecurityEvaluator>();

using var host = builder.Build();
var provider = host.Services;

var runtime = provider.GetRequiredService<RuntimeHandle>();
var report = RuntimeArchitectureLoader.Load(runtime, EngineeringArchitectureManifest.KnownArtifacts);
if (!report.IsValid)
{
    Console.Error.WriteLine($"Startup validation failed: {report.Failures.Count} registration failure(s).");
    foreach (var failure in report.Failures)
    {
        Console.Error.WriteLine($"  {failure}");
    }

    return CliExitCodeMapper.SoftwareError;
}

try
{
    return Run(args, provider);
}
catch (Exception)
{
    Console.Error.WriteLine("An unexpected error occurred.");
    return CliExitCodeMapper.SoftwareError;
}

static int Run(string[] args, IServiceProvider provider)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Usage: aef-cli <execute|capabilities> [options]");
        return CliExitCodeMapper.UsageError;
    }

    var command = args[0];
    var flags = ParseFlags(args.Skip(1).ToArray());

    return command switch
    {
        "execute" => RunExecute(flags, provider),
        "capabilities" => RunCapabilities(flags, provider),
        _ => UnknownCommand(command),
    };
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'. Expected 'execute' or 'capabilities'.");
    return CliExitCodeMapper.UsageError;
}

static Dictionary<string, string?> ParseFlags(string[] args)
{
    var flags = new Dictionary<string, string?>();
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var name = args[i][2..];
        var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : null;
        flags[name] = value;
    }

    return flags;
}

static int RunExecute(Dictionary<string, string?> flags, IServiceProvider provider)
{
    var output = flags.GetValueOrDefault("output", "text")!;
    var cliArguments = new CliExecuteArguments(
        flags.GetValueOrDefault("user-request", string.Empty)!,
        flags.GetValueOrDefault("project-profile-id", string.Empty)!,
        flags.GetValueOrDefault("session-id"),
        flags.GetValueOrDefault("payload-json"),
        output);

    var validationError = CliExecuteArgumentsValidator.Validate(cliArguments);
    if (validationError is not null)
    {
        Console.Error.WriteLine(validationError);
        return CliExitCodeMapper.UsageError;
    }

    var securityEvaluator = provider.GetRequiredService<ISecurityEvaluator>();
    var decision = securityEvaluator.Evaluate(new SecurityContext(Environment.UserName, null));
    if (!decision.IsAllowed)
    {
        Console.Error.WriteLine(decision.DenialReason ?? "Access denied.");
        return CliExitCodeMapper.SecurityDenied;
    }

    IReadOnlyDictionary<string, object>? payload = cliArguments.PayloadJson is null
        ? null
        : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(cliArguments.PayloadJson);

    var agent = provider.GetRequiredService<IAgentExecutionInterface>();
    var request = new ExecutionRequest(Guid.NewGuid().ToString("N"), cliArguments.SessionId, cliArguments.UserRequest, cliArguments.ProjectProfileId, payload);
    var response = agent.Execute(request);

    Console.WriteLine(CliOutputFormatter.FormatExecutionResponse(response, output));
    return CliExitCodeMapper.MapExecutionStatus(response.Status);
}

static int RunCapabilities(Dictionary<string, string?> flags, IServiceProvider provider)
{
    var output = flags.GetValueOrDefault("output", "text")!;
    var agent = provider.GetRequiredService<IAgentExecutionInterface>();
    Console.WriteLine(CliOutputFormatter.FormatCapabilities(agent.DiscoverCapabilities(), output));
    return 0;
}
```

- [ ] **Step 4: Add to the solution and build**

Run: `dotnet sln AefPlatform.sln add src/Cli/AefPlatform.Cli.csproj`
Run: `dotnet build AefPlatform.sln`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 5: Manually smoke-test both commands**

Run: `dotnet src/Cli/bin/Debug/net8.0/AefPlatform.Cli.dll capabilities`
Expected: exit code `0`, 8 lines of capability output printed to stdout. Check the exit code with `echo $?` (bash) immediately after.

Run: `dotnet src/Cli/bin/Debug/net8.0/AefPlatform.Cli.dll execute --user-request "start a new feature" --project-profile-id default-profile`
Expected: exit code `0`, output contains `Status: Completed`.

Run: `dotnet src/Cli/bin/Debug/net8.0/AefPlatform.Cli.dll execute --user-request ""  --project-profile-id default-profile`
Expected: exit code `64`, a usage-error message on stderr.

- [ ] **Step 6: Commit**

```bash
git add src/Cli AefPlatform.sln
git commit -m "feat(cli): add the Cli executable with execute and capabilities commands"
```

---

### Task 5: End-to-end process tests

**Files:**
- Create: `tests/Cli.Tests/Cli.Tests.csproj`
- Create: `tests/Cli.Tests/CliExecutableFixture.cs`
- Create: `tests/Cli.Tests/ExecuteCommandTests.cs`
- Create: `tests/Cli.Tests/CapabilitiesCommandTests.cs`

**Interfaces:**
- Consumes: the built `AefPlatform.Cli.dll` (Task 4), launched via `System.Diagnostics.Process`.

- [ ] **Step 1: Create the test project**

Create `tests/Cli.Tests/Cli.Tests.csproj`:

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
    <ProjectReference Include="..\..\src\Cli\AefPlatform.Cli.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create the shared executable-locating fixture**

Create `tests/Cli.Tests/CliExecutableFixture.cs`:

```csharp
using System.Diagnostics;

namespace Cli.Tests;

public static class CliExecutableFixture
{
    public static string LocateCliExecutablePath()
    {
        var testBinDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var configurationSegment = testBinDirectory.Parent!.Name;
        var targetFrameworkSegment = testBinDirectory.Name;

        var repositoryRoot = testBinDirectory;
        while (repositoryRoot is not null && !File.Exists(Path.Combine(repositoryRoot.FullName, "AefPlatform.sln")))
        {
            repositoryRoot = repositoryRoot.Parent;
        }

        if (repositoryRoot is null)
        {
            throw new InvalidOperationException("Could not locate AefPlatform.sln walking up from the test's base directory.");
        }

        var cliExecutablePath = Path.Combine(repositoryRoot.FullName, "src", "Cli", "bin", configurationSegment, targetFrameworkSegment, "AefPlatform.Cli.dll");
        if (!File.Exists(cliExecutablePath))
        {
            throw new FileNotFoundException($"AefPlatform.Cli.dll not found at '{cliExecutablePath}' - ensure the Cli project has been built.", cliExecutablePath);
        }

        return cliExecutablePath;
    }

    public static (int ExitCode, string StandardOutput, string StandardError) Run(params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet", string.Join(' ', new[] { $"\"{LocateCliExecutablePath()}\"" }.Concat(args.Select(a => $"\"{a}\""))))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout, stderr);
    }
}
```

- [ ] **Step 3: Write the E2E tests**

Create `tests/Cli.Tests/ExecuteCommandTests.cs`:

```csharp
namespace Cli.Tests;

public class ExecuteCommandTests
{
    [Fact]
    public void Execute_DeterministicCapability_ExitsZeroAndPrintsCompleted()
    {
        var (exitCode, stdout, _) = CliExecutableFixture.Run("execute", "--user-request", "start a new feature", "--project-profile-id", "default-profile");

        Assert.Equal(0, exitCode);
        Assert.Contains("Completed", stdout);
    }

    [Fact]
    public void Execute_UnknownSessionId_ExitsOne()
    {
        var (exitCode, _, _) = CliExecutableFixture.Run("execute", "--user-request", "start a new feature", "--project-profile-id", "default-profile", "--session-id", "does-not-exist");

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Execute_EmptyUserRequest_ExitsWithUsageError()
    {
        var (exitCode, _, stderr) = CliExecutableFixture.Run("execute", "--user-request", "", "--project-profile-id", "default-profile");

        Assert.Equal(64, exitCode);
        Assert.NotEmpty(stderr);
    }
}
```

Create `tests/Cli.Tests/CapabilitiesCommandTests.cs`:

```csharp
namespace Cli.Tests;

public class CapabilitiesCommandTests
{
    [Fact]
    public void Capabilities_ExitsZeroAndListsEightCapabilities_TextMode()
    {
        var (exitCode, stdout, _) = CliExecutableFixture.Run("capabilities");

        Assert.Equal(0, exitCode);
        Assert.Equal(8, stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void Capabilities_JsonMode_IsAnEightElementArray()
    {
        var (exitCode, stdout, _) = CliExecutableFixture.Run("capabilities", "--output", "json");

        Assert.Equal(0, exitCode);
        using var document = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.Equal(8, document.RootElement.GetArrayLength());
    }
}
```

- [ ] **Step 4: Add to the solution and run**

Run: `dotnet sln AefPlatform.sln add tests/Cli.Tests/Cli.Tests.csproj`
Run: `dotnet build AefPlatform.sln`
Run: `dotnet test tests/Cli.Tests/Cli.Tests.csproj`
Expected: all 5 tests PASS. If `LocateCliExecutablePath` throws `FileNotFoundException`, confirm `src/Cli` was actually built in the same configuration the test runner used (Step 4's `dotnet build` should have already produced it).

- [ ] **Step 5: Commit**

```bash
git add tests/Cli.Tests AefPlatform.sln
git commit -m "test(cli): add end-to-end process tests for execute and capabilities commands"
```

---

### Task 6: Full regression run and documentation

**Files:**
- Modify: `README.md`
- Create: `docs/cli/cli-adapter-reference.md`

- [ ] **Step 1: Run the full solution test suite**

Run: `dotnet test AefPlatform.sln`
Expected: all tests pass — 419 pre-existing plus this Capability's new tests (4 fitness + 5 validator + 2 exit-code + 4 formatter in `Adapters.Cli.Tests` = 15, plus 5 in `Cli.Tests`), 439+ total, 0 failures.

- [ ] **Step 2: Write the CLI reference**

Create `docs/cli/cli-adapter-reference.md`:

```markdown
# CLI Adapter Reference

Binary: `dotnet src/Cli/bin/<Configuration>/net8.0/AefPlatform.Cli.dll` (or the published apphost, `AefPlatform.Cli`/`AefPlatform.Cli.exe`, once published).

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
```

- [ ] **Step 3: Update `README.md`'s Status section**

In `README.md`, find the line starting with:

```
**AEF v1.0 GA-3 Capability 2 (REST Adapter) complete, 2026-07-08.**
```

Add a new paragraph immediately **before** it:

```markdown
**AEF v1.0 GA-3 Capability 3 (CLI Adapter) complete, 2026-07-08.** Third Capability of the External Execution Channels program — see `docs/superpowers/specs/2026-07-08-ga3-capability3-cli-adapter-design.md` for the full design and `docs/cli/cli-adapter-reference.md` for the command reference. Extracted a new `src/Composition` (`AefPlatform.Composition`) library out of `src/Host` for six dependency-light classes (`RuntimeArchitectureLoader`, `EngineeringArchitectureManifest`, `PlatformHostOptions`, `AiProviderOptions`, `PermissiveActivationPolicyResolver`, `TrustedDefaultSecurityEvaluator`) — namespace unchanged (`AefPlatform.Host`), zero behavior change, so `Cli` can reuse them without inheriting `Host`'s ASP.NET Core/Swashbuckle footprint. New `src/Adapters.Cli` (Runtime.Agent-only, fitness-tested) and `src/Cli` (`AefPlatform.Cli`, a second composition root alongside `Host`, deliberately duplicating `Host`'s DI-wiring sequence rather than sharing it — an accepted, documented tradeoff) together deliver a short-lived CLI executable with `execute` and `capabilities` commands, `sysexits.h`-based exit codes (0/1/2 domain-specific, 64/70/77 for usage/software/permission errors), and the same security-seam-before-execution invariant Capability 2 established for REST. No `Runtime.Ai`, `Runtime.Agent`'s existing contracts, `EngineeringExecutionPipeline`, or Capability 1/2 behavior changes. Full suite: 439+ passing.

**AEF v1.0 GA-3 Capability 2 (REST Adapter) complete, 2026-07-08.**
```

- [ ] **Step 4: Commit and push**

```bash
git add README.md docs/cli/cli-adapter-reference.md
git commit -m "docs: record GA-3 Capability 3 (CLI Adapter) completion and add the CLI reference"
git push -u origin ga3-capability3-cli-adapter-design
```
