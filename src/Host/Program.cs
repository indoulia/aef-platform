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

builder.Services.AddSingleton(_ => RuntimeBootstrapper.Boot(
    new RuntimeConfiguration(PolicyResolver: new PermissiveActivationPolicyResolver())));
builder.Services.AddHostedService<RuntimeStartupHostedService>();

var host = builder.Build();

await host.RunAsync();
