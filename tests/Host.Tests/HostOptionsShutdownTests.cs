using AefPlatform.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Host.Tests;

public class HostOptionsShutdownTests
{
    [Fact]
    public void ShutdownTimeout_ConfiguredFromRestAdapterOptions()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RestAdapter:ShutdownTimeoutSeconds"] = "45",
        });

        var restOptions = builder.Configuration.GetSection("RestAdapter").Get<RestAdapterOptions>() ?? new RestAdapterOptions();
        builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(restOptions.ShutdownTimeoutSeconds));

        using var host = builder.Build();
        var hostOptions = host.Services.GetRequiredService<IOptions<HostOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(45), hostOptions.ShutdownTimeout);
    }

    [Fact]
    public void ShutdownTimeout_DefaultsTo30Seconds()
    {
        var options = new RestAdapterOptions();

        Assert.Equal(30, options.ShutdownTimeoutSeconds);
    }
}
