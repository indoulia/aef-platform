using System.Diagnostics;
using Kernel.Governance;
using Kernel.Registry;
using Runtime.Bootstrapping;
using Runtime.DomainContent.ConversationManagement;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.DomainContent.EngineeringPlanning;
using Runtime.DomainContent.RepositoryIntelligence;
using Runtime.DomainContent.RepositoryValidation;
using Runtime.Execution;
using Runtime.Hosts.Skill;
using Runtime.Integration.DeliveryExecution;

namespace Runtime.Tests.Execution;

public class EngineeringExecutionPipelineTests
{
    private static RuntimeHandle BootWithBehaviors(IReadOnlyDictionary<string, IBehavior> behaviors) =>
        RuntimeBootstrapper.Boot(new RuntimeConfiguration(PolicyResolver: new AllowAllPolicyResolver()), behaviors);

    private static IReadOnlyDictionary<string, IBehavior> ConversationBehaviors(
        IReadOnlyDictionary<string, IBehavior>? extra = null)
    {
        var behaviors = new Dictionary<string, IBehavior>
        {
            ["SKL-EXE-0021"] = new ConversationIntakeAndRoutingBehavior(),
            ["SKL-EXE-0022"] = new ConversationHandoffAndFollowupBehavior(),
        };
        if (extra is not null)
        {
            foreach (var (id, behavior) in extra)
            {
                behaviors[id] = behavior;
            }
        }
        return behaviors;
    }

    [Fact]
    public void Execute_FeatureRequest_RoutesAndDispatchesToFeatureLifecycleOrchestrator()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("ORC-EXE-0001", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("WFL-DEL-0001", "Delivery"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("AGT-EXE-0002", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("Please implement the new feature", "default-profile"));

        Assert.True(result.Success);
        Assert.Equal("WFL-DEL-0001", result.ExecutedWorkflowId);
    }

    [Fact]
    public void Execute_EpicRequest_RoutesAndDispatchesToEpicLifecycleOrchestrator()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("ORC-EXE-0001", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("WFL-DEL-0004", "Delivery"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("AGT-EXE-0006", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("Start the epic for onboarding", "default-profile"));

        Assert.True(result.Success);
        Assert.Equal("WFL-DEL-0004", result.ExecutedWorkflowId);
    }

    [Fact]
    public void Execute_UnroutableRequest_ReturnsFailureWithClarificationDiagnostics()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("What time is it?", "default-profile"));

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains("I need more information", result.PresentedResult);
    }

    [Fact]
    public void Execute_UnroutableRequest_SetsKindToNeedsClarification()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("What time is it?", "default-profile"));

        Assert.Equal(ExecutionResultKind.NeedsClarification, result.Kind);
    }

    [Fact]
    public void Execute_DeliveryRequest_DispatchesThroughSkillHostAndReturnsValidationResults()
    {
        var deliveryBehaviors = new Dictionary<string, IBehavior>
        {
            ["SKL-EXE-0018"] = new DeliveryImplementationSkillBehavior(_ => new AlwaysVerifiedVerifier()),
        };
        var runtime = BootWithBehaviors(ConversationBehaviors(deliveryBehaviors));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0018", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var payload = new Dictionary<string, object>
        {
            ["ImplementationRequest"] = new ImplementationRequest("story-1", "feature/x", new[] { "abc123" }, "plan"),
            ["RepositoryRoot"] = "C:\\fake-repo",
        };
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("Please deliver the fix", "default-profile", payload));

        Assert.True(result.Success);
        Assert.Equal("AGT-EXE-0010", result.ExecutedAgentId);
        Assert.NotNull(result.ValidationResults);
    }

    [Fact]
    public void Execute_WorkflowNotRegistered_ReturnsFailureDiagnostics()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("implement a feature", "default-profile"));

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Execute_WorkflowNotRegistered_KindStaysUnspecified()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("implement a feature", "default-profile"));

        Assert.Equal(ExecutionResultKind.Unspecified, result.Kind);
    }

    [Fact]
    public void Execute_AnalyzeRequest_DispatchesToRepositoryIntelligence()
    {
        var root = InitDotnetGitRepository();
        try
        {
            var repoIntelBehaviors = new Dictionary<string, IBehavior>
            {
                ["SKL-EXE-0030"] = new RepositoryAndArchitectureUnderstandingBehavior(),
                ["SKL-EXE-0031"] = new CodeIntelligenceAndQualitySignalDetectionBehavior(),
                ["SKL-EXE-0033"] = new RecommendationSynthesisAndEvidenceBasedReportingBehavior(),
            };
            var runtime = BootWithBehaviors(ConversationBehaviors(repoIntelBehaviors));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0030", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0031", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0033", "Execution"));
            var pipeline = new EngineeringExecutionPipeline(runtime);
            var payload = new Dictionary<string, object> { ["RepositoryName"] = "test-repo", ["RepositoryRoot"] = root };

            var result = pipeline.Execute(new EngineeringRequest("Please analyze the repository", "default-profile", payload));

            Assert.True(result.Success);
            Assert.Equal("AGT-EXE-0015", result.ExecutedAgentId);
            Assert.Equal("test-repo", result.RepositoryAnalyzed);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void Execute_AnalyzeRequest_WithReportedProblem_SurfacesDiagnosticsConfidenceInResult()
    {
        var root = InitDotnetGitRepository();
        try
        {
            var repoIntelBehaviors = new Dictionary<string, IBehavior>
            {
                ["SKL-EXE-0030"] = new RepositoryAndArchitectureUnderstandingBehavior(),
                ["SKL-EXE-0031"] = new CodeIntelligenceAndQualitySignalDetectionBehavior(),
                ["SKL-EXE-0032"] = new EngineeringDiagnosticsAndRootCauseAnalysisBehavior(),
                ["SKL-EXE-0033"] = new RecommendationSynthesisAndEvidenceBasedReportingBehavior(),
            };
            var runtime = BootWithBehaviors(ConversationBehaviors(repoIntelBehaviors));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0030", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0031", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0032", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0033", "Execution"));
            var pipeline = new EngineeringExecutionPipeline(runtime);
            var payload = new Dictionary<string, object>
            {
                ["RepositoryName"] = "test-repo",
                ["RepositoryRoot"] = root,
                ["ReportedProblem"] = "Startup throws NullReferenceException",
                ["EvidenceSources"] = (IReadOnlyList<string>)new[] { "log excerpt A" },
            };

            var result = pipeline.Execute(new EngineeringRequest("Please analyze the repository", "default-profile", payload));

            Assert.True(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Contains("confidence", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void Execute_PlanRequest_DispatchesToEngineeringPlanning()
    {
        var root = InitDotnetGitRepository();
        try
        {
            var planningBehaviors = new Dictionary<string, IBehavior>
            {
                ["SKL-EXE-0023"] = new EngineeringDiscoveryAndGoalClarificationBehavior(),
                ["SKL-EXE-0024"] = new SpecificationDraftingAndAlignmentBehavior(),
                ["SKL-EXE-0025"] = new ReadinessClassificationAndApprovalRequestBehavior(),
            };
            var runtime = BootWithBehaviors(ConversationBehaviors(planningBehaviors));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0023", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0024", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0025", "Execution"));
            var pipeline = new EngineeringExecutionPipeline(runtime);
            var payload = new Dictionary<string, object> { ["RepositoryName"] = "test-repo", ["RepositoryRoot"] = root, ["Goal"] = "Add a new API endpoint" };

            var result = pipeline.Execute(new EngineeringRequest("Plan the specification for a new capability", "default-profile", payload));

            Assert.True(result.Success);
            Assert.Equal("AGT-EXE-0012", result.ExecutedAgentId);
            Assert.Equal("test-repo", result.RepositoryAnalyzed);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void Execute_ValidateRequest_DispatchesToRepositoryValidation()
    {
        var root = InitDotnetGitRepository();
        try
        {
            var validationBehaviors = new Dictionary<string, IBehavior>
            {
                ["REPO-VALIDATION"] = new RepositoryValidationSkillBehavior(new StubVerificationRunner(new VerificationRunResult(0, "passed", string.Empty))),
            };
            var runtime = BootWithBehaviors(ConversationBehaviors(validationBehaviors));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("REPO-VALIDATION", "Execution"));
            var pipeline = new EngineeringExecutionPipeline(runtime);
            var payload = new Dictionary<string, object> { ["RepositoryName"] = "test-repo", ["RepositoryRoot"] = root };

            var result = pipeline.Execute(new EngineeringRequest("Validate the build", "default-profile", payload));

            Assert.True(result.Success);
            Assert.Equal("AGT-EXE-0010", result.ExecutedAgentId);
            Assert.True(result.TestsExecuted);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void Execute_ValidateRequest_RealTestFailure_ReportsFailure_NeverFabricatesASuccess()
    {
        var root = InitDotnetGitRepository();
        try
        {
            var validationBehaviors = new Dictionary<string, IBehavior>
            {
                ["REPO-VALIDATION"] = new RepositoryValidationSkillBehavior(new StubVerificationRunner(new VerificationRunResult(1, string.Empty, "1 test failed"))),
            };
            var runtime = BootWithBehaviors(ConversationBehaviors(validationBehaviors));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("REPO-VALIDATION", "Execution"));
            var pipeline = new EngineeringExecutionPipeline(runtime);
            var payload = new Dictionary<string, object> { ["RepositoryName"] = "test-repo", ["RepositoryRoot"] = root };

            var result = pipeline.Execute(new EngineeringRequest("Validate the build", "default-profile", payload));

            Assert.False(result.Success);
            Assert.True(result.TestsExecuted);
            Assert.NotEmpty(result.Diagnostics);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void Execute_ValidateRequest_NoRepositoryPayloadSupplied_ReturnsClarificationDiagnostic_NeverAFabricatedEmptyRepo()
    {
        var validationBehaviors = new Dictionary<string, IBehavior>
        {
            ["REPO-VALIDATION"] = new RepositoryValidationSkillBehavior(new StubVerificationRunner(new VerificationRunResult(0, string.Empty, string.Empty))),
        };
        var runtime = BootWithBehaviors(ConversationBehaviors(validationBehaviors));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("REPO-VALIDATION", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("Validate the build", "default-profile"));

        Assert.False(result.Success);
        Assert.Null(result.RepositoryAnalyzed);
        Assert.Contains(result.Diagnostics, d => d.Contains("No repository context was supplied"));
    }

    [Fact]
    public void Execute_ValidateRequest_EmptyStringRepositoryPayloadValues_ReturnsClarificationDiagnostic_NeverThrows()
    {
        // Regression: TryGetRepositoryContext only checked "is string",
        // not IsNullOrEmpty - an empty (not missing) RepositoryRoot/Name
        // used to flow into RepositoryContextFactory.Create, which throws
        // ArgumentException from LocalFileSystemRepositoryScanner/
        // LocalGitOperations's own constructors, crashing the pipeline
        // instead of degrading to this method's own documented "never a
        // fabricated empty context" clarification diagnostic.
        var validationBehaviors = new Dictionary<string, IBehavior>
        {
            ["REPO-VALIDATION"] = new RepositoryValidationSkillBehavior(new StubVerificationRunner(new VerificationRunResult(0, string.Empty, string.Empty))),
        };
        var runtime = BootWithBehaviors(ConversationBehaviors(validationBehaviors));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("REPO-VALIDATION", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);
        var payload = new Dictionary<string, object> { ["RepositoryName"] = "test-repo", ["RepositoryRoot"] = string.Empty };

        var result = pipeline.Execute(new EngineeringRequest("Validate the build", "default-profile", payload));

        Assert.False(result.Success);
        Assert.Null(result.RepositoryAnalyzed);
        Assert.Contains(result.Diagnostics, d => d.Contains("No repository context was supplied"));
    }

    [Fact]
    public void Execute_ValidateRequest_DirtyWorkingTree_ReportsFilesChanged()
    {
        var root = InitDotnetGitRepository();
        try
        {
            File.WriteAllText(Path.Combine(root, "new-file.txt"), "content");
            var validationBehaviors = new Dictionary<string, IBehavior>
            {
                ["REPO-VALIDATION"] = new RepositoryValidationSkillBehavior(new StubVerificationRunner(new VerificationRunResult(0, "passed", string.Empty))),
            };
            var runtime = BootWithBehaviors(ConversationBehaviors(validationBehaviors));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
            runtime.Kernel.Registry.Register(new DefinitionSubmission("REPO-VALIDATION", "Execution"));
            var pipeline = new EngineeringExecutionPipeline(runtime);
            var payload = new Dictionary<string, object> { ["RepositoryName"] = "test-repo", ["RepositoryRoot"] = root };

            var result = pipeline.Execute(new EngineeringRequest("Validate the build", "default-profile", payload));

            Assert.NotNull(result.FilesChanged);
            Assert.Contains("new-file.txt", result.FilesChanged!);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    private sealed class StubCodeIntelligence : Runtime.Integration.CodeIntelligence.ICodeIntelligenceProvider
    {
        private static Runtime.Integration.CodeIntelligence.CodeLookupResult One(string name, string file, int line) =>
            Runtime.Integration.CodeIntelligence.CodeLookupResult.Found(new[] { new Runtime.Integration.CodeIntelligence.CodeReference(name, "class", file, line, null, "stub") });

        private static Runtime.Integration.CodeIntelligence.CodeLookupResult None =>
            Runtime.Integration.CodeIntelligence.CodeLookupResult.Found(Array.Empty<Runtime.Integration.CodeIntelligence.CodeReference>());

        public Runtime.Integration.CodeIntelligence.CodeLookupResult FindRelevant(string repositoryRoot, string taskDescription, int maxResults) => None;

        public Runtime.Integration.CodeIntelligence.CodeLookupResult FindSymbol(string repositoryRoot, string name, int maxResults) =>
            name == "OrderService" ? One("OrderService", "src/OrderService.cs", 12) : None;

        public Runtime.Integration.CodeIntelligence.CodeLookupResult FindTests(string repositoryRoot, string file) => None;

        public Runtime.Integration.CodeIntelligence.CodeLookupResult FindAffected(string repositoryRoot, IReadOnlyList<string> changedFiles) =>
            changedFiles.Count == 0 ? None : One("AppTests.cs", "tests/AppTests.cs", 0);
    }

    private static EngineeringExecutionPipeline GroundingPipeline(IVerificationRunner? runner = null)
    {
        var behaviors = new Dictionary<string, IBehavior>
        {
            ["PROMPT-GROUNDING"] = new Runtime.DomainContent.PromptGrounding.PromptGroundingSkillBehavior(new StubCodeIntelligence()),
            ["REPO-VALIDATION"] = new RepositoryValidationSkillBehavior(runner ?? new StubVerificationRunner(new VerificationRunResult(0, "passed", string.Empty))),
            ["CHANGE-CHECK"] = new Runtime.DomainContent.ChangeCheck.ChangeCheckSkillBehavior(new StubCodeIntelligence()),
        };
        var runtime = BootWithBehaviors(ConversationBehaviors(behaviors));
        foreach (var id in new[] { "SKL-EXE-0021", "SKL-EXE-0022", "PROMPT-GROUNDING", "REPO-VALIDATION", "CHANGE-CHECK" })
        {
            runtime.Kernel.Registry.Register(new DefinitionSubmission(id, "Execution"));
        }

        return new EngineeringExecutionPipeline(runtime);
    }

    private static Dictionary<string, object> ChangeCheckPayload(string root) =>
        new() { ["RepositoryName"] = "test-repo", ["RepositoryRoot"] = root };

    [Fact]
    public void Execute_ChangeCheck_ListsChangedFiles_RunsValidation_AndMarksChecks()
    {
        var root = InitDotnetGitRepository();
        try
        {
            File.WriteAllText(Path.Combine(root, "New.cs"), "class New {}");
            var payload = ChangeCheckPayload(root);
            payload["AcceptanceChecks"] = "`dotnet test` exits 0\nChanges are confined to the files under Where to work, or each extra file is explained\nThe retry count is configurable";
            payload["ExpectedFiles"] = "app.csproj";

            var result = GroundingPipeline().Execute(new EngineeringRequest("check change", "default-profile", payload));

            Assert.True(result.Success);
            Assert.Equal("CHANGE-CHECK", result.ExecutedWorkflowId);
            Assert.Equal(new[] { "New.cs" }, result.FilesChanged);
            Assert.True(result.TestsExecuted);
            Assert.Contains("- [met] `dotnet test` exits 0", result.Document);
            Assert.Contains("- [not met] Changes are confined", result.Document);
            Assert.Contains("- [cannot tell] The retry count is configurable", result.Document);
            Assert.Contains(result.Diagnostics, d => d.Contains("New.cs"));
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void Execute_ChangeCheck_FailingBuild_ReportsFailure()
    {
        var root = InitDotnetGitRepository();
        try
        {
            File.WriteAllText(Path.Combine(root, "New.cs"), "class New {}");

            var result = GroundingPipeline(new StubVerificationRunner(new VerificationRunResult(1, string.Empty, "1 test failed")))
                .Execute(new EngineeringRequest("check change", "default-profile", ChangeCheckPayload(root)));

            Assert.False(result.Success);
            Assert.Contains("FAILED", result.Document);
            Assert.Contains("1 test failed", result.Document);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    private sealed class ThrowingVerificationRunner : IVerificationRunner
    {
        public VerificationRunResult Run(string workingDirectory, string command, IReadOnlyList<string> arguments) =>
            throw new Xunit.Sdk.XunitException("The build and tests must not be run.");
    }

    [Fact]
    public void Execute_ChangeCheck_SkipBuildAndTests_DoesNotRunValidation()
    {
        var root = InitDotnetGitRepository();
        try
        {
            File.WriteAllText(Path.Combine(root, "New.cs"), "class New {}");
            var payload = ChangeCheckPayload(root);
            payload["SkipBuildAndTests"] = "true";

            var result = GroundingPipeline(new ThrowingVerificationRunner()).Execute(new EngineeringRequest("check change", "default-profile", payload));

            Assert.True(result.Success);
            Assert.False(result.TestsExecuted);
            Assert.Contains("Build and tests were not run.", result.Document);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void Execute_ChangeCheck_NoChanges_DoesNotRunValidation_AndSaysNoChanges()
    {
        var root = InitDotnetGitRepository();
        try
        {
            var result = GroundingPipeline(new ThrowingVerificationRunner()).Execute(new EngineeringRequest("check change", "default-profile", ChangeCheckPayload(root)));

            Assert.True(result.Success);
            Assert.Contains("No changed files were found", result.Document);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void Execute_ChangeCheck_NotAGitRepository_FailsSayingSo_NeverThrows()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var result = GroundingPipeline().Execute(new EngineeringRequest("check change", "default-profile", ChangeCheckPayload(root)));

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Contains("not a git repository"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Execute_PromptGrounding_ReturnsSuccessWithDocumentContainingVerifiedFactsHeading()
    {
        var root = InitDotnetGitRepository();
        try
        {
            var payload = new Dictionary<string, object> { ["RepositoryName"] = "test-repo", ["RepositoryRoot"] = root, ["UserPrompt"] = "Add retries to OrderService and PaymentGateway" };

            var result = GroundingPipeline().Execute(new EngineeringRequest("refine prompt", "default-profile", payload));

            Assert.True(result.Success);
            Assert.Equal("AGT-EXE-0015", result.ExecutedAgentId);
            Assert.Equal("test-repo", result.RepositoryAnalyzed);
            Assert.Contains("## Verified facts", result.Document);
            Assert.Contains("- `OrderService` is a class (src/OrderService.cs:12)", result.Document);
            Assert.Contains(result.Diagnostics, d => d.Contains("`PaymentGateway`") && d.Contains("not found"));
            Assert.Contains("Every open question above is answered before editing", result.ValidationResults!);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Execute_PromptGrounding_EmptyUserPrompt_FailsAskingForAPrompt(string? prompt)
    {
        var root = InitDotnetGitRepository();
        try
        {
            var payload = new Dictionary<string, object> { ["RepositoryName"] = "test-repo", ["RepositoryRoot"] = root };
            if (prompt is not null)
            {
                payload["UserPrompt"] = prompt;
            }

            var result = GroundingPipeline().Execute(new EngineeringRequest("refine prompt", "default-profile", payload));

            Assert.False(result.Success);
            Assert.Null(result.Document);
            Assert.Contains(result.Diagnostics, d => d.Contains("UserPrompt"));
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void Execute_PromptGrounding_NoRepositoryPayload_ReturnsMissingRepositoryContext()
    {
        var payload = new Dictionary<string, object> { ["UserPrompt"] = "Add retries to OrderService" };

        var result = GroundingPipeline().Execute(new EngineeringRequest("refine prompt", "default-profile", payload));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Contains("RepositoryName and RepositoryRoot"));
    }

    [Fact]
    public void Execute_PromptGrounding_UnreadableRepositoryRoot_FailsSayingSo()
    {
        var payload = new Dictionary<string, object> { ["RepositoryName"] = "x", ["RepositoryRoot"] = "C:/does/not/exist/anywhere", ["UserPrompt"] = "Add retries to OrderService" };

        var result = GroundingPipeline().Execute(new EngineeringRequest("refine prompt", "default-profile", payload));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Contains("not readable"));
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

        // Execution-discovered flake (Epic 3 dogfooding sweep): a file
        // inside a just-created git repository can still be momentarily
        // locked by another process under full-suite parallel test runs -
        // Directory.Delete throws IOException in that window. Retry with a
        // short backoff rather than failing the test over a transient
        // OS-level lock (same fix already applied to Host.Tests'
        // RepositoryEngineeringHostIntegrationTests.DeleteRepository).
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

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }

    private sealed class AlwaysVerifiedVerifier : IIsolatedChangeVerifier
    {
        public bool BranchExists(string branchName) => true;
        public bool IsDefaultBranch(string branchName) => false;
        public bool CommitExistsOnBranch(string branchName, string commitSha) => true;
    }

    private sealed class StubVerificationRunner : IVerificationRunner
    {
        private readonly VerificationRunResult _result;

        public StubVerificationRunner(VerificationRunResult result) => _result = result;

        public VerificationRunResult Run(string workingDirectory, string command, IReadOnlyList<string> arguments) => _result;
    }
}
