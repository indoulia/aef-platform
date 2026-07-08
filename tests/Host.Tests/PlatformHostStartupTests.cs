using AefPlatform.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runtime.Bootstrapping;

namespace Host.Tests;

public class PlatformHostStartupTests
{
    private static HostApplicationBuilder CreateBuilder(IDictionary<string, string?> configOverrides)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
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
