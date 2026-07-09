using System.Diagnostics;
using AefPlatform.Host;
using Microsoft.Extensions.Configuration;
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

namespace Host.Tests;

public class RepositoryEngineeringHostIntegrationTests
{
    private static void ConfigureBuilder(HostApplicationBuilder builder)
    {
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
                ["SKL-EXE-0023"] = new EngineeringDiscoveryAndGoalClarificationBehavior(),
                ["SKL-EXE-0024"] = new SpecificationDraftingAndAlignmentBehavior(),
                ["SKL-EXE-0025"] = new ReadinessClassificationAndApprovalRequestBehavior(),
                ["SKL-EXE-0030"] = new RepositoryAndArchitectureUnderstandingBehavior(),
                ["SKL-EXE-0031"] = new CodeIntelligenceAndQualitySignalDetectionBehavior(),
                ["SKL-EXE-0032"] = new EngineeringDiagnosticsAndRootCauseAnalysisBehavior(),
                ["SKL-EXE-0033"] = new RecommendationSynthesisAndEvidenceBasedReportingBehavior(),
                ["REPO-VALIDATION"] = new RepositoryValidationSkillBehavior(),
            };
            return RuntimeBootstrapper.Boot(new RuntimeConfiguration(PolicyResolver: new PermissiveActivationPolicyResolver()), behaviors);
        });
        builder.Services.AddSingleton(sp => new EngineeringExecutionPipeline(sp.GetRequiredService<RuntimeHandle>()));
        builder.Services.AddHostedService<RuntimeStartupHostedService>();
    }

    [Fact]
    public async Task Execute_AnalyzeRequest_AgainstTheRealBootedHost_DispatchesToRepositoryIntelligence()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        ConfigureBuilder(builder);

        using var host = builder.Build();
        await host.StartAsync();

        var root = InitDotnetGitRepository();
        try
        {
            var pipeline = host.Services.GetRequiredService<EngineeringExecutionPipeline>();
            var payload = new Dictionary<string, object> { ["RepositoryName"] = "test-repo", ["RepositoryRoot"] = root };

            var result = pipeline.Execute(new EngineeringRequest("Please analyze the repository", "default-profile", payload));

            Assert.True(result.Success);
            Assert.Equal("AGT-EXE-0015", result.ExecutedAgentId);
            Assert.Equal("test-repo", result.RepositoryAnalyzed);
        }
        finally
        {
            DeleteRepository(root);
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Execute_PlanRequest_AgainstTheRealBootedHost_DispatchesToEngineeringPlanning()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        ConfigureBuilder(builder);

        using var host = builder.Build();
        await host.StartAsync();

        var root = InitDotnetGitRepository();
        try
        {
            var pipeline = host.Services.GetRequiredService<EngineeringExecutionPipeline>();
            var payload = new Dictionary<string, object> { ["RepositoryName"] = "test-repo", ["RepositoryRoot"] = root, ["Goal"] = "Add a new API endpoint" };

            var result = pipeline.Execute(new EngineeringRequest("Plan the specification for a new capability", "default-profile", payload));

            Assert.True(result.Success);
            Assert.Equal("AGT-EXE-0012", result.ExecutedAgentId);
        }
        finally
        {
            DeleteRepository(root);
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Execute_ValidateRequest_AgainstTheRealBootedHost_DispatchesToRepositoryValidation()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        ConfigureBuilder(builder);

        using var host = builder.Build();
        await host.StartAsync();

        var root = InitDotnetGitRepository();
        try
        {
            var pipeline = host.Services.GetRequiredService<EngineeringExecutionPipeline>();
            var payload = new Dictionary<string, object> { ["RepositoryName"] = "test-repo", ["RepositoryRoot"] = root };

            var result = pipeline.Execute(new EngineeringRequest("Validate the build", "default-profile", payload));

            // A real `dotnet test` run against a bare .csproj marker file with
            // no test framework is expected to fail (non-zero exit) - this
            // proves the real ProcessVerificationRunner actually executed the
            // real detected command, never a fabricated pass (Success can be
            // either value; TestsExecuted must be true either way).
            Assert.True(result.TestsExecuted);
        }
        finally
        {
            DeleteRepository(root);
            await host.StopAsync();
        }
    }

    private static string InitDotnetGitRepository()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        RunGit(root, "init", "-b", "main");
        RunGit(root, "config", "user.email", "test@example.com");
        RunGit(root, "config", "user.name", "Test User");
        File.WriteAllText(Path.Combine(root, "app.csproj"), "<Project />");
        RunGit(root, "add", "-A");
        RunGit(root, "commit", "-m", "initial");
        return root;
    }

    private static void RunGit(string root, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in arguments) startInfo.ArgumentList.Add(arg);
        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
    }

    private static void DeleteRepository(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        // Execution-discovered flake (dogfooding sweep): a file inside a
        // just-created git repository can still be momentarily locked by
        // another process (observed under full-suite parallel test runs,
        // not reproducible running this class alone) - Directory.Delete
        // throws IOException in that window. Retry with a short backoff
        // rather than failing the test over a transient OS-level lock.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(root, true);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(100 * attempt);
            }
        }
    }
}
