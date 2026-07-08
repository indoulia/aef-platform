using AefPlatform.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runtime.Bootstrapping;
using Runtime.DomainContent.ConversationManagement;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.DomainContent.EngineeringPlanning;
using Runtime.DomainContent.RepositoryIntelligence;
using Runtime.DomainContent.RepositoryValidation;
using Runtime.Execution;
using Runtime.Hosts.Skill;

var builder = Host.CreateApplicationBuilder(args);

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
        ["SKL-EXE-0023"] = new EngineeringDiscoveryAndGoalClarificationBehavior(),
        ["SKL-EXE-0024"] = new SpecificationDraftingAndAlignmentBehavior(),
        ["SKL-EXE-0025"] = new ReadinessClassificationAndApprovalRequestBehavior(),
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
builder.Services.AddHostedService<RuntimeStartupHostedService>();

var host = builder.Build();

await host.RunAsync();
