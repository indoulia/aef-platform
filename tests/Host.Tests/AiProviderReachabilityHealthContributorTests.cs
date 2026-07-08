using System.Net;
using System.Net.Sockets;
using AefPlatform.Host;
using Microsoft.Extensions.Options;
using Runtime.Agent.Health;

namespace Host.Tests;

public class AiProviderReachabilityHealthContributorTests
{
    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task Check_ReachableUrl_ReturnsHealthy()
    {
        var port = GetFreeLoopbackPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var serverTask = Task.Run(() =>
        {
            var context = listener.GetContext();
            context.Response.StatusCode = 200;
            context.Response.OutputStream.Close();
        });

        var options = Options.Create(new AiProviderOptions
        {
            Provider = "OpenAI",
            OpenAi = new AiProviderOptions.OpenAiSection { BaseUrl = $"http://127.0.0.1:{port}/" },
        });
        var contributor = new AiProviderReachabilityHealthContributor(new HttpClient(), options);

        var report = contributor.Check();

        Assert.Equal(RuntimeHealthStatus.Healthy, report.Status);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(2));
        listener.Stop();
    }

    [Fact]
    public void Check_UnreachablePort_ReturnsUnhealthy()
    {
        var options = Options.Create(new AiProviderOptions
        {
            Provider = "OpenAI",
            OpenAi = new AiProviderOptions.OpenAiSection { BaseUrl = "http://127.0.0.1:1/" },
        });
        var contributor = new AiProviderReachabilityHealthContributor(new HttpClient(), options);

        var report = contributor.Check();

        Assert.Equal(RuntimeHealthStatus.Unhealthy, report.Status);
    }

    [Fact]
    public void Check_NoBaseUrlConfigured_ReturnsUnhealthy()
    {
        var options = Options.Create(new AiProviderOptions { Provider = "OpenAI", OpenAi = null });
        var contributor = new AiProviderReachabilityHealthContributor(new HttpClient(), options);

        var report = contributor.Check();

        Assert.Equal(RuntimeHealthStatus.Unhealthy, report.Status);
    }
}
