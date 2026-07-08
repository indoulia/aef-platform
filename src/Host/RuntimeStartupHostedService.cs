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
