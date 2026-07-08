using AefPlatform.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runtime.Bootstrapping;
using Runtime.DomainContent.ConversationManagement;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.Execution;
using Runtime.Hosts.Skill;

namespace Host.Tests;

public class EngineeringExecutionPipelineHostIntegrationTests
{
    [Fact]
    public async Task Execute_FeatureRequest_AgainstTheRealBootedHost_DispatchesSuccessfully()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AefPlatform:PlatformName"] = "AEF Platform Runtime",
        });

        builder.Services
            .AddOptions<PlatformHostOptions>()
            .Bind(builder.Configuration.GetSection("AefPlatform"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton(_ =>
        {
            IReadOnlyDictionary<string, IBehavior> behaviors = new Dictionary<string, IBehavior>
            {
                ["SKL-EXE-0018"] = new DeliveryImplementationSkillBehavior(),
                ["SKL-EXE-0019"] = new DeliveryValidationSkillBehavior(),
                ["SKL-EXE-0020"] = new DeliveryDocumentationSkillBehavior(),
                ["SKL-EXE-0021"] = new ConversationIntakeAndRoutingBehavior(),
                ["SKL-EXE-0022"] = new ConversationHandoffAndFollowupBehavior(),
            };
            return RuntimeBootstrapper.Boot(new RuntimeConfiguration(PolicyResolver: new PermissiveActivationPolicyResolver()), behaviors);
        });
        builder.Services.AddSingleton(sp => new EngineeringExecutionPipeline(sp.GetRequiredService<RuntimeHandle>()));
        builder.Services.AddHostedService<RuntimeStartupHostedService>();

        using var host = builder.Build();
        await host.StartAsync();

        var pipeline = host.Services.GetRequiredService<EngineeringExecutionPipeline>();
        var result = pipeline.Execute(new EngineeringRequest("Please implement the new feature", "default-profile"));

        Assert.True(result.Success);
        Assert.Equal("WFL-DEL-0001", result.ExecutedWorkflowId);

        await host.StopAsync();
    }
}
