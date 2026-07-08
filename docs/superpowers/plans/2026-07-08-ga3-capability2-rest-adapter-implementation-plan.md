# GA-3 Capability 2: REST Adapter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement GA-3 Capability 2 per `docs/superpowers/specs/2026-07-08-ga3-capability2-rest-adapter-design.md`: a new `Adapters.Rest` project (Runtime.Agent-only dependency) exposing five HTTP endpoints, hosted inside `AefPlatform.Host` via `WebApplication`, invoking the security seam before every execution.

**Architecture:** `Adapters.Rest` (class library, `FrameworkReference Microsoft.AspNetCore.App`, `ProjectReference Runtime.Agent` only) contains the endpoint-mapping extension method, request DTOs, validation, and security-context translation. `Host` switches from `Host.CreateApplicationBuilder` to `WebApplication.CreateBuilder`, adds `RestAdapterOptions`, Swashbuckle, a `ProblemDetails` exception handler, and maps `Adapters.Rest`'s endpoints under a configurable base path.

**Tech Stack:** .NET 8, ASP.NET Core Minimal APIs (via `Microsoft.NET.Sdk.Web`), `Swashbuckle.AspNetCore` (OpenAPI), `Microsoft.AspNetCore.Mvc.Testing` (test-only), xUnit.

## Global Constraints

- Target framework: `net8.0` everywhere — no project's target framework changes.
- `Adapters.Rest.csproj` references `Runtime.Agent` (`ProjectReference`) and `Microsoft.AspNetCore.App` (`FrameworkReference`) only — no `Runtime`, `Runtime.Ai`, or `Kernel` reference, enforced by a fitness test (Task 6).
- No existing GA-1/GA-2/Capability-1 contract or file changes. `Host`'s SDK/`Program.cs`/`.csproj` changes are additive only — no existing DI registration line is altered or removed.
- `EngineeringExecutionPipeline` is not modified.
- Health endpoint returns a fixed `{"status":"Healthy"}` — no real contributor logic (Capability 4's scope).
- No Non-Goal item appears anywhere: no CLI/Chat/IDE/SDK/batch/streaming/SSE/WebSockets/auth-providers/OAuth/JWT/rate-limiting/caching/product-specific-endpoint code.
- The full existing test suite (400 tests through Capability 1) must stay green throughout — re-run as the final gate (Task 8).

---

### Task 1: Create the `Adapters.Rest` project with request/response DTOs

**Files:**
- Create: `src/Adapters.Rest/Adapters.Rest.csproj`
- Create: `src/Adapters.Rest/RestExecutionRequest.cs`
- Create: `src/Adapters.Rest/RestStartSessionRequest.cs`
- Create: `src/Adapters.Rest/RestHealthResponse.cs`
- Create: `src/Adapters.Rest/RestVersionResponse.cs`
- Test: `tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj` (create)
- Test: `tests/Adapters.Rest.Tests/ArchitectureBoundaryTests.cs` (create)

**Interfaces:**
- Produces: `Adapters.Rest.RestExecutionRequest(string? SessionId, string UserRequest, string ProjectProfileId, IReadOnlyDictionary<string,object>? Payload)`, `Adapters.Rest.RestStartSessionRequest(string? ProjectProfileId)`, `Adapters.Rest.RestHealthResponse(string Status)`, `Adapters.Rest.RestVersionResponse(string Version)` — consumed by Task 2's endpoint handlers.

- [ ] **Step 1: Create the project file**

Create `src/Adapters.Rest/Adapters.Rest.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Runtime.Agent\Runtime.Agent.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create the DTOs**

Create `src/Adapters.Rest/RestExecutionRequest.cs`:

```csharp
namespace Adapters.Rest;

public sealed record RestExecutionRequest(string? SessionId, string UserRequest, string ProjectProfileId, IReadOnlyDictionary<string, object>? Payload);
```

Create `src/Adapters.Rest/RestStartSessionRequest.cs`:

```csharp
namespace Adapters.Rest;

public sealed record RestStartSessionRequest(string? ProjectProfileId);
```

Create `src/Adapters.Rest/RestHealthResponse.cs`:

```csharp
namespace Adapters.Rest;

public sealed record RestHealthResponse(string Status);
```

Create `src/Adapters.Rest/RestVersionResponse.cs`:

```csharp
namespace Adapters.Rest;

public sealed record RestVersionResponse(string Version);
```

- [ ] **Step 3: Create the test project and the fitness test**

Create `tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj`:

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
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Adapters.Rest\Adapters.Rest.csproj" />
    <ProjectReference Include="..\..\src\Host\AefPlatform.Host.csproj" />
    <ProjectReference Include="..\..\src\Runtime.Agent\Runtime.Agent.csproj" />
  </ItemGroup>

</Project>
```

Create `tests/Adapters.Rest.Tests/ArchitectureBoundaryTests.cs` (mirrors `Runtime.Agent.Tests/ArchitectureBoundaryTests.cs`'s exclusion pattern):

```csharp
namespace Adapters.Rest.Tests;

public class ArchitectureBoundaryTests
{
    private static IReadOnlyList<string?> AdaptersRestReferencedAssemblyNames() =>
        typeof(RestExecutionRequest).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

    [Fact]
    public void AdaptersRestAssembly_DoesNotReferenceRuntime()
    {
        Assert.DoesNotContain("Runtime", AdaptersRestReferencedAssemblyNames());
    }

    [Fact]
    public void AdaptersRestAssembly_DoesNotReferenceRuntimeAi()
    {
        Assert.DoesNotContain("Runtime.Ai", AdaptersRestReferencedAssemblyNames());
    }

    [Fact]
    public void AdaptersRestAssembly_DoesNotReferenceKernel()
    {
        Assert.DoesNotContain("Kernel", AdaptersRestReferencedAssemblyNames());
    }

    [Fact]
    public void AdaptersRestAssembly_ReferencesRuntimeAgent()
    {
        Assert.Contains("Runtime.Agent", AdaptersRestReferencedAssemblyNames());
    }
}
```

- [ ] **Step 4: Build to confirm the new projects compile and the fitness test passes**

Run: `dotnet build AefPlatform.sln`
Expected: Build succeeded (the new projects aren't referenced by the solution's default build target list automatically — if `dotnet build AefPlatform.sln` doesn't pick up the new `.csproj` files, run `dotnet sln AefPlatform.sln add src/Adapters.Rest/Adapters.Rest.csproj tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj` first, then rebuild).

Run: `dotnet test tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj --filter "FullyQualifiedName~ArchitectureBoundaryTests"`
Expected: all 4 tests PASS (the assembly currently has no other code, so it trivially satisfies the exclusions; `ReferencesRuntimeAgent` passes because `Adapters.Rest.csproj`'s `ProjectReference` is genuinely used — no code references a `Runtime.Agent` type yet in this Task, so re-verify this specific assertion again after Task 2 adds real usage; if it fails now, that's expected and will be fixed by Task 2, not a blocker to proceeding).

- [ ] **Step 5: Commit**

```bash
git add src/Adapters.Rest tests/Adapters.Rest.Tests AefPlatform.sln
git commit -m "feat(adapters.rest): scaffold the Adapters.Rest project and REST DTOs"
```

---

### Task 2: Request validation and security-context translation

**Files:**
- Create: `src/Adapters.Rest/RestExecutionRequestValidator.cs`
- Create: `src/Adapters.Rest/RestSecurityContextFactory.cs`
- Test: `tests/Adapters.Rest.Tests/RestExecutionRequestValidatorTests.cs` (create)

**Interfaces:**
- Consumes: `Adapters.Rest.RestExecutionRequest` (Task 1), `Runtime.Agent.Security.SecurityContext` (Capability 1).
- Produces: `RestExecutionRequestValidator.Validate(RestExecutionRequest) : string?` (null = valid, else a human-readable error), `RestSecurityContextFactory.FromHttpContext(HttpContext) : SecurityContext` — both consumed by Task 3's endpoint handlers.

- [ ] **Step 1: Write the failing tests**

Create `tests/Adapters.Rest.Tests/RestExecutionRequestValidatorTests.cs`:

```csharp
namespace Adapters.Rest.Tests;

public class RestExecutionRequestValidatorTests
{
    [Fact]
    public void Validate_ValidRequest_ReturnsNull()
    {
        var request = new RestExecutionRequest(null, "start a new feature", "default-profile", null);

        Assert.Null(RestExecutionRequestValidator.Validate(request));
    }

    [Fact]
    public void Validate_EmptyUserRequest_ReturnsError()
    {
        var request = new RestExecutionRequest(null, "", "default-profile", null);

        Assert.NotNull(RestExecutionRequestValidator.Validate(request));
    }

    [Fact]
    public void Validate_WhitespaceUserRequest_ReturnsError()
    {
        var request = new RestExecutionRequest(null, "   ", "default-profile", null);

        Assert.NotNull(RestExecutionRequestValidator.Validate(request));
    }

    [Fact]
    public void Validate_EmptyProjectProfileId_ReturnsError()
    {
        var request = new RestExecutionRequest(null, "start a new feature", "", null);

        Assert.NotNull(RestExecutionRequestValidator.Validate(request));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj --filter "FullyQualifiedName~RestExecutionRequestValidatorTests"`
Expected: build FAILS — `RestExecutionRequestValidator` does not exist.

- [ ] **Step 3: Implement the validator and the security-context factory**

Create `src/Adapters.Rest/RestExecutionRequestValidator.cs`:

```csharp
namespace Adapters.Rest;

public static class RestExecutionRequestValidator
{
    public static string? Validate(RestExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserRequest))
        {
            return "UserRequest must not be empty.";
        }

        if (string.IsNullOrWhiteSpace(request.ProjectProfileId))
        {
            return "ProjectProfileId must not be empty.";
        }

        return null;
    }
}
```

Create `src/Adapters.Rest/RestSecurityContextFactory.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Runtime.Agent.Security;

namespace Adapters.Rest;

public static class RestSecurityContextFactory
{
    public static SecurityContext FromHttpContext(HttpContext httpContext) =>
        new(PrincipalId: httpContext.User.Identity?.Name, Claims: null);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj --filter "FullyQualifiedName~RestExecutionRequestValidatorTests"`
Expected: all 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Adapters.Rest/RestExecutionRequestValidator.cs src/Adapters.Rest/RestSecurityContextFactory.cs tests/Adapters.Rest.Tests/RestExecutionRequestValidatorTests.cs
git commit -m "feat(adapters.rest): add request validation and security-context translation"
```

---

### Task 3: Endpoint-mapping extension method

**Files:**
- Create: `src/Adapters.Rest/RestAdapterEndpointExtensions.cs`

**Interfaces:**
- Consumes: `Runtime.Agent.IAgentExecutionInterface`, `Runtime.Agent.Security.ISecurityEvaluator` (resolved from DI by minimal-API parameter binding), `RestExecutionRequestValidator.Validate`, `RestSecurityContextFactory.FromHttpContext` (Task 2), all four DTOs (Task 1).
- Produces: `Adapters.Rest.RestAdapterEndpointExtensions.MapRestAdapterEndpoints(this RouteGroupBuilder) : RouteGroupBuilder` — consumed by Task 4's `Host` wiring.

No isolated unit test for this task — it is exercised end-to-end by Task 4's `WebApplicationFactory`-based endpoint tests, which is the only way to meaningfully test a minimal-API handler's parameter binding and DI resolution (per "Task Right-Sizing": this task's own deliverable has no testable behavior in isolation from a running host).

- [ ] **Step 1: Implement the endpoint mapping**

Create `src/Adapters.Rest/RestAdapterEndpointExtensions.cs`:

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Runtime.Agent;
using Runtime.Agent.Security;

namespace Adapters.Rest;

public static class RestAdapterEndpointExtensions
{
    // AgentExecutionService.UnknownSessionResponse's own literal reason
    // string (Runtime.Agent, frozen) - duplicated here rather than
    // promoted to a shared constant, since editing a frozen GA-2 file for
    // a robustness nicety (not a bug) is out of this Capability's scope
    // (design Decision 9's "Accepted risk"). If this string is ever
    // renamed, the 404-mapping test in ExecutionEndpointTests fails
    // loudly against the real AgentExecutionService - not a silent break.
    private const string UnknownSessionReason = "Unknown session.";

    public static RouteGroupBuilder MapRestAdapterEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/execution", Execute);
        group.MapGet("/capabilities", DiscoverCapabilities);
        group.MapPost("/sessions", StartSession);
        group.MapGet("/health", Health);
        group.MapGet("/version", Version);
        return group;
    }

    private static IResult Execute(RestExecutionRequest request, IAgentExecutionInterface agent, ISecurityEvaluator securityEvaluator, HttpContext httpContext)
    {
        var validationError = RestExecutionRequestValidator.Validate(request);
        if (validationError is not null)
        {
            return Results.Problem(detail: validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        var decision = securityEvaluator.Evaluate(RestSecurityContextFactory.FromHttpContext(httpContext));
        if (!decision.IsAllowed)
        {
            return Results.Problem(detail: decision.DenialReason ?? "Access denied.", statusCode: StatusCodes.Status403Forbidden);
        }

        var executionRequest = new ExecutionRequest(Guid.NewGuid().ToString("N"), request.SessionId, request.UserRequest, request.ProjectProfileId, request.Payload);
        var response = agent.Execute(executionRequest);

        if (response.Status == ExecutionStatus.Failed && response.Error?.Reason == UnknownSessionReason)
        {
            return Results.Json(response, statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Ok(response);
    }

    private static IResult DiscoverCapabilities(IAgentExecutionInterface agent) => Results.Ok(agent.DiscoverCapabilities());

    private static IResult StartSession(RestStartSessionRequest request, IAgentExecutionInterface agent)
    {
        if (request.ProjectProfileId is not null && string.IsNullOrWhiteSpace(request.ProjectProfileId))
        {
            return Results.Problem(detail: "ProjectProfileId must not be whitespace when supplied.", statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Json(agent.StartSession(request.ProjectProfileId), statusCode: StatusCodes.Status201Created);
    }

    private static IResult Health() => Results.Ok(new RestHealthResponse("Healthy"));

    private static IResult Version() => Results.Ok(new RestVersionResponse(GetInformationalVersion()));

    private static string GetInformationalVersion()
    {
        var assembly = typeof(RestAdapterEndpointExtensions).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
```

- [ ] **Step 2: Build to confirm it compiles**

Run: `dotnet build src/Adapters.Rest/Adapters.Rest.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Re-run the Task 1 fitness tests — `ReferencesRuntimeAgent` should now genuinely pass**

Run: `dotnet test tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj --filter "FullyQualifiedName~ArchitectureBoundaryTests"`
Expected: all 4 tests PASS, including `AdaptersRestAssembly_ReferencesRuntimeAgent` (now genuinely exercised by `Execute`'s use of `ExecutionRequest`/`ExecutionStatus`).

- [ ] **Step 4: Commit**

```bash
git add src/Adapters.Rest/RestAdapterEndpointExtensions.cs
git commit -m "feat(adapters.rest): add the endpoint-mapping extension method"
```

---

### Task 4: Wire the REST Adapter into `Host`

**Files:**
- Modify: `src/Host/AefPlatform.Host.csproj`
- Modify: `src/Host/Program.cs`
- Create: `src/Host/RestAdapterOptions.cs`
- Modify: `src/Host/appsettings.json`

**Interfaces:**
- Consumes: `Adapters.Rest.RestAdapterEndpointExtensions.MapRestAdapterEndpoints` (Task 3).
- Produces: a live `WebApplication` hosting all five REST endpoints under the configured base path — consumed by Task 5/6/7's `WebApplicationFactory<Program>`-based tests.

- [ ] **Step 1: Switch the project SDK and add the new package/project references**

Replace the full contents of `src/Host/AefPlatform.Host.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AefPlatform.Host</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Options.DataAnnotations" Version="8.0.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Runtime\Runtime.csproj" />
    <ProjectReference Include="..\Runtime.Agent\Runtime.Agent.csproj" />
    <ProjectReference Include="..\Adapters.Rest\Adapters.Rest.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Add `RestAdapterOptions`**

Create `src/Host/RestAdapterOptions.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace AefPlatform.Host;

public sealed class RestAdapterOptions
{
    [Range(1, 65535)]
    public int Port { get; set; } = 5080;

    public string BasePath { get; set; } = "/api/v1";

    public long MaxRequestBodyBytes { get; set; } = 1_048_576;
}
```

- [ ] **Step 3: Add the `RestAdapter` section to `appsettings.json`**

In `src/Host/appsettings.json`, find:

```json
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
```

Replace with:

```json
  "RestAdapter": {
    "Port": 5080,
    "BasePath": "/api/v1",
    "MaxRequestBodyBytes": 1048576
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
```

- [ ] **Step 4: Update `Program.cs`**

In `src/Host/Program.cs`, find the top `using` block and the first line of code:

```csharp
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

var builder = Host.CreateApplicationBuilder(args);
```

Replace with:

```csharp
using Adapters.Rest;
using AefPlatform.Host;
using Microsoft.AspNetCore.Builder;
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

var builder = WebApplication.CreateBuilder(args);

var restOptions = builder.Configuration.GetSection("RestAdapter").Get<RestAdapterOptions>() ?? new RestAdapterOptions();

builder.Services
    .AddOptions<RestAdapterOptions>()
    .Bind(builder.Configuration.GetSection("RestAdapter"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = restOptions.MaxRequestBodyBytes);
builder.WebHost.UseUrls($"http://localhost:{restOptions.Port}");

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
```

Then find the end of the file:

```csharp
builder.Services.AddSingleton<ISecurityEvaluator, TrustedDefaultSecurityEvaluator>();
builder.Services.AddHostedService<RuntimeStartupHostedService>();

var host = builder.Build();

await host.RunAsync();
```

Replace with:

```csharp
builder.Services.AddSingleton<ISecurityEvaluator, TrustedDefaultSecurityEvaluator>();
builder.Services.AddHostedService<RuntimeStartupHostedService>();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        await Results.Problem(detail: "An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError)
            .ExecuteAsync(context);
    });
});

app.UseSwagger();
app.UseSwaggerUI();

app.MapGroup(restOptions.BasePath).MapRestAdapterEndpoints();

await app.RunAsync();

public partial class Program { }
```

- [ ] **Step 5: Build the whole solution**

Run: `dotnet build AefPlatform.sln`
Expected: Build succeeded, 0 warnings, 0 errors. If `Swashbuckle.AspNetCore` version `6.6.2` fails to restore, check https://www.nuget.org for the latest `6.x` release compatible with `net8.0` and substitute that version number — this is the only step in this plan where an exact package version might need adjusting at implementation time.

- [ ] **Step 6: Run the full existing suite to confirm no regression from the `WebApplication` switch**

Run: `dotnet test AefPlatform.sln`
Expected: all pre-existing tests still pass (400 from before this Capability) — the `WebApplication` change is additive and must not break `Host.Tests`' own DI-graph tests, which duplicate the builder logic independently and are unaffected by `Program.cs`'s own builder type.

- [ ] **Step 7: Commit**

```bash
git add src/Host/AefPlatform.Host.csproj src/Host/Program.cs src/Host/RestAdapterOptions.cs src/Host/appsettings.json
git commit -m "feat(host): host the REST Adapter via WebApplication"
```

---

### Task 5: Execution endpoint tests (happy path, 404, validation, security denial)

**Files:**
- Test: `tests/Adapters.Rest.Tests/ExecutionEndpointTests.cs` (create)

**Interfaces:**
- Consumes: `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>`, `Adapters.Rest.RestExecutionRequest`, `Runtime.Agent.ExecutionResponse`, `Runtime.Agent.ExecutionStatus`, `Runtime.Agent.Security.ISecurityEvaluator`/`SecurityContext`/`SecurityDecision`.

- [ ] **Step 1: Write the tests**

Create `tests/Adapters.Rest.Tests/ExecutionEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Runtime.Agent;
using Runtime.Agent.Security;

namespace Adapters.Rest.Tests;

public class ExecutionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ExecutionEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task PostExecution_RoutesToADeterministicCapability_ReturnsOkCompleted()
    {
        using var client = _factory.CreateClient();
        var request = new RestExecutionRequest(null, "start a new feature", "default-profile", null);

        var response = await client.PostAsJsonAsync("/api/v1/execution", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExecutionResponse>();
        Assert.NotNull(body);
        Assert.Equal(ExecutionStatus.Completed, body!.Status);
    }

    [Fact]
    public async Task PostExecution_UnknownSessionId_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var request = new RestExecutionRequest("does-not-exist", "start a new feature", "default-profile", null);

        var response = await client.PostAsJsonAsync("/api/v1/execution", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostExecution_EmptyUserRequest_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        var request = new RestExecutionRequest(null, "", "default-profile", null);

        var response = await client.PostAsJsonAsync("/api/v1/execution", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostExecution_MalformedJson_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent("{ this is not valid json", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/execution", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostExecution_UnsupportedMediaType_ReturnsUnsupportedMediaType()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent("plain text body", System.Text.Encoding.UTF8, "text/plain");

        var response = await client.PostAsync("/api/v1/execution", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    private sealed class SpyDenyAllSecurityEvaluator : ISecurityEvaluator
    {
        public int CallCount { get; private set; }

        public SecurityDecision Evaluate(SecurityContext context)
        {
            CallCount++;
            return new SecurityDecision(IsAllowed: false, DenialReason: "Denied by test double.");
        }
    }

    [Fact]
    public async Task PostExecution_SecurityDenies_ReturnsForbiddenAndNeverCallsRuntimeAgent()
    {
        var spyEvaluator = new SpyDenyAllSecurityEvaluator();
        using var factoryWithDenial = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ISecurityEvaluator>(spyEvaluator);
            });
        });
        using var client = factoryWithDenial.CreateClient();
        var request = new RestExecutionRequest(null, "start a new feature", "default-profile", null);

        var response = await client.PostAsJsonAsync("/api/v1/execution", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, spyEvaluator.CallCount);
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj --filter "FullyQualifiedName~ExecutionEndpointTests"`
Expected: all 6 tests PASS. If `PostExecution_RoutesToADeterministicCapability_ReturnsOkCompleted` fails with a non-`Completed` status, inspect the response body's `Error`/`PresentedResult` — `ConversationIntakeAndRoutingBehavior`'s keyword table routes `"start a new feature"` (containing `"feature"`) to `WFL-DEL-0004`... verify against `src/Runtime/DomainContent/ConversationManagement/ConversationIntakeAndRoutingBehavior.cs`'s exact keyword-to-capability table before assuming the test is wrong; adjust the `UserRequest` string to match whichever keyword deterministically reaches a `Completed` outcome with no AI call and no repository payload if the exact routing differs from this assumption.

- [ ] **Step 3: Commit**

```bash
git add tests/Adapters.Rest.Tests/ExecutionEndpointTests.cs
git commit -m "test(adapters.rest): add execution endpoint tests (happy path, 404, validation, security denial)"
```

---

### Task 6: Capabilities, sessions, health, version, and OpenAPI endpoint tests

**Files:**
- Test: `tests/Adapters.Rest.Tests/CapabilitiesEndpointTests.cs` (create)
- Test: `tests/Adapters.Rest.Tests/SessionsEndpointTests.cs` (create)
- Test: `tests/Adapters.Rest.Tests/HealthEndpointTests.cs` (create)
- Test: `tests/Adapters.Rest.Tests/VersionEndpointTests.cs` (create)
- Test: `tests/Adapters.Rest.Tests/OpenApiEndpointTests.cs` (create)

**Interfaces:**
- Consumes: same `WebApplicationFactory<Program>` fixture pattern as Task 5.

- [ ] **Step 1: Write the tests**

Create `tests/Adapters.Rest.Tests/CapabilitiesEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Runtime.Agent.Capabilities;

namespace Adapters.Rest.Tests;

public class CapabilitiesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CapabilitiesEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetCapabilities_ReturnsTheHandAuthoredEightCapabilities()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/capabilities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<CapabilityDescriptor>>();
        Assert.NotNull(body);
        Assert.Equal(8, body!.Count);
    }
}
```

Create `tests/Adapters.Rest.Tests/SessionsEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Runtime.Agent;

namespace Adapters.Rest.Tests;

public class SessionsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SessionsEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task PostSessions_ReturnsCreatedWithANonEmptySessionId()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/sessions", new RestStartSessionRequest(null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExecutionSession>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body!.SessionId));
    }
}
```

Create `tests/Adapters.Rest.Tests/HealthEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Adapters.Rest.Tests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetHealth_ReturnsOkHealthy()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RestHealthResponse>();
        Assert.Equal("Healthy", body!.Status);
    }
}
```

Create `tests/Adapters.Rest.Tests/VersionEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Adapters.Rest.Tests;

public class VersionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public VersionEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetVersion_ReturnsOkWithNonEmptyVersion()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RestVersionResponse>();
        Assert.False(string.IsNullOrEmpty(body!.Version));
    }
}
```

Create `tests/Adapters.Rest.Tests/OpenApiEndpointTests.cs`:

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Adapters.Rest.Tests;

public class OpenApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetSwaggerJson_ReturnsOkValidJson()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("paths", out _));
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test tests/Adapters.Rest.Tests/Adapters.Rest.Tests.csproj --filter "FullyQualifiedName~CapabilitiesEndpointTests|FullyQualifiedName~SessionsEndpointTests|FullyQualifiedName~HealthEndpointTests|FullyQualifiedName~VersionEndpointTests|FullyQualifiedName~OpenApiEndpointTests"`
Expected: all 5 tests PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/Adapters.Rest.Tests/CapabilitiesEndpointTests.cs tests/Adapters.Rest.Tests/SessionsEndpointTests.cs tests/Adapters.Rest.Tests/HealthEndpointTests.cs tests/Adapters.Rest.Tests/VersionEndpointTests.cs tests/Adapters.Rest.Tests/OpenApiEndpointTests.cs
git commit -m "test(adapters.rest): add capabilities, sessions, health, version, and OpenAPI endpoint tests"
```

---

### Task 7: Full regression run and documentation

**Files:**
- Modify: `README.md`
- Create: `docs/api/rest-adapter-reference.md`

**Interfaces:**
- Consumes/Produces: nothing new — verification and documentation only.

- [ ] **Step 1: Run the full solution test suite**

Run: `dotnet test AefPlatform.sln`
Expected: all tests pass — 400 pre-existing plus this Capability's new tests (4 architecture + 4 validator + 6 execution + 5 capabilities/sessions/health/version/openapi = 19 new), 419+ total, 0 failures.

- [ ] **Step 2: Write the REST API reference**

Create `docs/api/rest-adapter-reference.md`:

```markdown
# REST Adapter API Reference

Base path: `/api/v1` (configurable via `RestAdapter:BasePath`, `appsettings.json`).

## `POST /api/v1/execution`

Request body:
```json
{ "sessionId": null, "userRequest": "start a new feature", "projectProfileId": "default-profile", "payload": null }
```

Response: `Runtime.Agent.ExecutionResponse` (camelCase JSON), `200 OK` for `Completed`/`NeedsClarification`/most `Failed` outcomes, `404 Not Found` when `sessionId` names an unknown session, `400 Bad Request` for empty `userRequest`/`projectProfileId` or malformed JSON, `415 Unsupported Media Type` for a non-JSON `Content-Type`, `403 Forbidden` if the security seam denies the request.

## `GET /api/v1/capabilities`

Returns the full `CapabilityDescriptor[]` catalog. Always `200 OK`.

## `POST /api/v1/sessions`

Request body: `{ "projectProfileId": null }`. Returns a new `Runtime.Agent.ExecutionSession`, `201 Created`.

## `GET /api/v1/health`

Returns `{ "status": "Healthy" }` unconditionally, `200 OK`. Real health-contributor logic is Capability 4's chartered scope — this is an honest placeholder, not yet a live signal.

## `GET /api/v1/version`

Returns `{ "version": "<assembly informational version>" }`, `200 OK`.

## OpenAPI

`GET /swagger/v1/swagger.json` — machine-readable OpenAPI document. `GET /swagger` — interactive Swagger UI.

## Security

Every `/api/v1/execution` call passes through `ISecurityEvaluator` before `Runtime.Agent` is invoked. The trusted-default implementation authorizes every request — no production authentication exists yet (Non-Goal for this Capability).

## Configuration

`appsettings.json`'s `RestAdapter` section: `Port` (default `5080`), `BasePath` (default `/api/v1`), `MaxRequestBodyBytes` (default `1048576`).
```

- [ ] **Step 3: Update `README.md`'s Status section**

In `README.md`, find the line starting with:

```
**AEF v1.0 GA-3 Capability 1 (AEF Runtime Host) complete, 2026-07-08.**
```

Add a new paragraph immediately **before** it (newest-first ordering — this entry is newer than Capability 1's):

```markdown
**AEF v1.0 GA-3 Capability 2 (REST Adapter) complete, 2026-07-08.** Second Capability of the External Execution Channels program — see `docs/superpowers/specs/2026-07-08-ga3-capability2-rest-adapter-design.md` for the full design and `docs/api/rest-adapter-reference.md` for the endpoint reference. New `src/Adapters.Rest` project references `Runtime.Agent` only (enforced by a fitness test, mirroring `Runtime.Agent`'s own `Runtime`-only boundary), containing five Minimal API endpoints (`POST /api/v1/execution`, `GET /api/v1/capabilities`, `POST /api/v1/sessions`, `GET /api/v1/health`, `GET /api/v1/version`) hosted inside the same `AefPlatform.Host` process Capability 1 built — `Program.cs` switches from `Host.CreateApplicationBuilder` to `WebApplication.CreateBuilder`, an additive change that leaves every existing DI registration untouched. Every execution request passes through Capability 1's `ISecurityEvaluator` seam before reaching `Runtime.Agent`. `ExecutionStatus` maps to HTTP status per a documented table (`Completed`/`NeedsClarification`/ordinary `Failed` → `200 OK`, the one `Failed` case with a stable discriminator — an unknown session — → `404 Not Found`, adapter-side validation failures → `400`/`415`, security denial → `403`, unhandled exceptions → `500` via `ProblemDetails`, never exposing internal exception details). OpenAPI documentation is generated by `Swashbuckle.AspNetCore` (`GET /swagger/v1/swagger.json`). Health endpoint is an honest, fixed `"Healthy"` placeholder — real contributor logic is Capability 4's chartered scope, not pulled forward here. No `Runtime.Ai`, `Runtime.Agent`'s existing contracts, or `EngineeringExecutionPipeline` change. Full suite: 419+ passing.

**AEF v1.0 GA-3 Capability 1 (AEF Runtime Host) complete, 2026-07-08.**
```

- [ ] **Step 4: Commit**

```bash
git add README.md docs/api/rest-adapter-reference.md
git commit -m "docs: record GA-3 Capability 2 (REST Adapter) completion and add the API reference"
```

- [ ] **Step 5: Push**

Run: `git push -u origin ga3-capability2-rest-adapter-design` (or the current branch name)
Expected: push succeeds.
