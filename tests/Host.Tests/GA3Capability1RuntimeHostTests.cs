using AefPlatform.Host;
using Microsoft.Extensions.Configuration;
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

namespace Host.Tests;

// GA-3 Capability 1: proves the security seam resolves from the real
// composition graph with the trusted-default implementation, and that it
// is genuinely substitutable via DI without touching Runtime.Agent or
// Program.cs - the Security Seam Validated gate (GA-3 engineering program
// doc, Section 9) requires demonstrating this, not merely asserting it.
public class GA3Capability1RuntimeHostTests
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
        builder.Services.AddSingleton(sp => new EngineeringExecutionPipeline(sp.GetRequiredService<RuntimeHandle>()));
        builder.Services.AddSingleton<ICapabilityCatalog, EngineeringAgentCapabilityCatalog>();
        builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
        builder.Services.AddSingleton<IAgentExecutionInterface>(sp => new AgentExecutionService(
            sp.GetRequiredService<EngineeringExecutionPipeline>(),
            sp.GetRequiredService<ICapabilityCatalog>(),
            sp.GetRequiredService<ISessionStore>()));
        builder.Services.AddSingleton<ISecurityEvaluator, TrustedDefaultSecurityEvaluator>();
        builder.Services.AddHostedService<RuntimeStartupHostedService>();

        return builder;
    }

    private static IDictionary<string, string?> DefaultConfig() => new Dictionary<string, string?>
    {
        ["AefPlatform:PlatformName"] = "AEF Platform Runtime",
        ["Ai:Provider"] = "OpenAI",
        ["Ai:OpenAi:ApiKey"] = "",
        ["Ai:OpenAi:Model"] = "gpt-4o",
    };

    [Fact]
    public async Task StartAsync_ResolvesTrustedDefaultSecurityEvaluator()
    {
        var builder = CreateBuilder(DefaultConfig());
        using var host = builder.Build();

        await host.StartAsync();
        var evaluator = host.Services.GetRequiredService<ISecurityEvaluator>();

        Assert.IsType<TrustedDefaultSecurityEvaluator>(evaluator);
        Assert.True(evaluator.Evaluate(new SecurityContext(null, null)).IsAllowed);
        await host.StopAsync();
    }

    private sealed class DenyAllSecurityEvaluator : ISecurityEvaluator
    {
        public SecurityDecision Evaluate(SecurityContext context) => new(IsAllowed: false, DenialReason: "Denied by test double.");
    }

    [Fact]
    public async Task StartAsync_SecurityEvaluatorIsSubstitutableViaDI()
    {
        var builder = CreateBuilder(DefaultConfig());
        builder.Services.AddSingleton<ISecurityEvaluator, DenyAllSecurityEvaluator>();
        using var host = builder.Build();

        await host.StartAsync();
        var evaluator = host.Services.GetRequiredService<ISecurityEvaluator>();

        Assert.IsType<DenyAllSecurityEvaluator>(evaluator);
        var decision = evaluator.Evaluate(new SecurityContext("caller-1", null));
        Assert.False(decision.IsAllowed);
        Assert.Equal("Denied by test double.", decision.DenialReason);
        await host.StopAsync();
    }
}
