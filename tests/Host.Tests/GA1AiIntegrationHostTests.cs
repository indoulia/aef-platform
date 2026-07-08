using AefPlatform.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
using Runtime.Hosts.Skill;

namespace Host.Tests;

// Mirrors Program.cs's own DI wiring (the composition root itself has no
// seams to unit-test directly, since it is top-level statements) so this
// proves the real registration graph - AiProviderOptions binding, the
// IAiProvider factory switch, and the four new behaviors resolving via
// AiExecutionService/IPromptCatalog - boots cleanly end-to-end.
public class GA1AiIntegrationHostTests
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
        builder.Services.AddHostedService<RuntimeStartupHostedService>();

        return builder;
    }

    [Fact]
    public async Task StartAsync_WithDefaultEmptyOpenAiKey_BootsSuccessfully()
    {
        // Construction must not throw even with an empty API key - only a
        // real HTTP call to OpenAI would fail, and this test never makes one.
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["AefPlatform:PlatformName"] = "AEF Platform Runtime",
            ["Ai:Provider"] = "OpenAI",
            ["Ai:OpenAi:ApiKey"] = "",
            ["Ai:OpenAi:Model"] = "gpt-4o",
        });
        using var host = builder.Build();

        var exception = await Record.ExceptionAsync(() => host.StartAsync());

        Assert.Null(exception);
        await host.StopAsync();
    }

    [Fact]
    public async Task StartAsync_RegistersTheFourNewAiBackedSkillsAndTheirWorkflowsAsActive()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["AefPlatform:PlatformName"] = "AEF Platform Runtime",
            ["Ai:Provider"] = "OpenAI",
            ["Ai:OpenAi:ApiKey"] = "",
            ["Ai:OpenAi:Model"] = "gpt-4o",
        });
        using var host = builder.Build();

        await host.StartAsync();

        var runtime = host.Services.GetRequiredService<RuntimeHandle>();
        foreach (var id in new[] { "AGT-EXE-0013", "SKL-EXE-0026", "WFL-DEL-0007", "AGT-EXE-0014", "SKL-EXE-0027", "SKL-EXE-0028", "SKL-EXE-0029", "WFL-DEL-0005" })
        {
            Assert.True(runtime.Kernel.Registry.Query(id).Found, $"{id} was not registered at startup.");
        }

        await host.StopAsync();
    }

    [Fact]
    public void SelectsAzureOpenAiProviderWhenConfigured()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["AefPlatform:PlatformName"] = "AEF Platform Runtime",
            ["Ai:Provider"] = "AzureOpenAI",
            ["Ai:AzureOpenAi:Endpoint"] = "https://example.openai.azure.com",
            ["Ai:AzureOpenAi:ApiKey"] = "",
            ["Ai:AzureOpenAi:Deployment"] = "gpt-4o-deployment",
        });
        using var host = builder.Build();

        var provider = host.Services.GetRequiredService<IAiProvider>();

        Assert.Equal("AzureOpenAI", provider.Name);
    }
}
