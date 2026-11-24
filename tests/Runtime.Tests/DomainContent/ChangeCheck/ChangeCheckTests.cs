using Runtime.Context;
using Runtime.DomainContent.ChangeCheck;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.Execution;
using Runtime.Integration.CodeIntelligence;
using Runtime.Integration.DeliveryExecution;
using Runtime.Integration.GitOperations;

namespace Runtime.Tests.DomainContent.ChangeCheck;

public class ChangeCheckTests
{
    private const string Confined = "Changes are confined to the files under Where to work, or each extra file is explained";

    private sealed class FakeProvider : ICodeIntelligenceProvider
    {
        public bool Available { get; set; } = true;

        public CodeLookupResult FindRelevant(string repositoryRoot, string taskDescription, int maxResults) => CodeLookupResult.Found(Array.Empty<CodeReference>());

        public CodeLookupResult FindSymbol(string repositoryRoot, string name, int maxResults) => CodeLookupResult.Found(Array.Empty<CodeReference>());

        public CodeLookupResult FindTests(string repositoryRoot, string file) => CodeLookupResult.Found(Array.Empty<CodeReference>());

        public CodeLookupResult FindAffected(string repositoryRoot, IReadOnlyList<string> changedFiles) => Available
            ? CodeLookupResult.Found(new[] { new CodeReference("ATests.cs", "test-file", "tests/ATests.cs", 0, null, "fake") })
            : CodeLookupResult.Unavailable("tokensave is not installed or not on PATH.");
    }

    private static readonly RepositoryExecutionContext Repository = new("C:/repo", "repo", true, true, "feature/x", false, null);

    private static ValidationOutcome Validation(int exitCode) =>
        ValidationOutcome.Recorded(new TestPlanAndResultsEntry("repo", exitCode == 0, exitCode, "out", exitCode == 0 ? string.Empty : "1 test failed"));

    private static RuntimeExecutionContext Context(Dictionary<string, object> content) =>
        new("change-check-test", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "profile", "ChangeCheck", content);

    private static ChangeCheckReport Execute(GitChangedFilesResult changes, ValidationOutcome? validation, string[]? checks = null, string[]? expected = null, FakeProvider? provider = null)
    {
        var request = new ChangeCheckRequest(Repository, changes, validation, checks ?? Array.Empty<string>(), expected ?? Array.Empty<string>());
        var output = new ChangeCheckSkillBehavior(provider ?? new FakeProvider()).Execute(Context(new Dictionary<string, object> { ["ChangeCheckRequest"] = request }));
        return Assert.IsType<ChangeCheckReport>(output);
    }

    private static GitChangedFilesResult Changed(params string[] files) => new(true, null, "merge-base with main (abc1234)", files);

    [Fact]
    public void Execute_FileOutsideExpected_IsListed_AndConfinedCheckIsNotMet()
    {
        var report = Execute(Changed("src/A.cs", "src/Other.cs"), Validation(0), new[] { Confined }, new[] { "src/A.cs", "tests/ATests.cs" });

        Assert.Equal(new[] { "src/Other.cs" }, report.FilesOutsideExpected);
        var check = Assert.Single(report.Checks);
        Assert.Equal("not met", check.Status);
        Assert.Contains("src/Other.cs", check.Evidence);
    }

    [Fact]
    public void Execute_AllFilesExpected_ConfinedCheckIsMet_IgnoringCaseAndSlashDirection()
    {
        var report = Execute(Changed("src/A.cs"), Validation(0), new[] { Confined }, new[] { "SRC\\a.cs" });

        Assert.Empty(report.FilesOutsideExpected);
        Assert.Equal("met", Assert.Single(report.Checks).Status);
    }

    [Fact]
    public void Execute_NoExpectedFilesSupplied_ConfinedCheckIsCannotTell()
    {
        var report = Execute(Changed("src/A.cs"), Validation(0), new[] { Confined });

        Assert.Equal("cannot tell", Assert.Single(report.Checks).Status);
    }

    [Fact]
    public void Execute_ValidationPassed_CommandCheckIsMet()
    {
        var report = Execute(Changed("src/A.cs"), Validation(0), new[] { "`dotnet test` exits 0", "Tests in tests/ATests.cs pass" });

        Assert.True(report.BuildAndTestsPassed);
        Assert.All(report.Checks, check => Assert.Equal("met", check.Status));
    }

    [Fact]
    public void Execute_ValidationFailed_CommandCheckIsNotMet_WithExitCode()
    {
        var report = Execute(Changed("src/A.cs"), Validation(1), new[] { "`dotnet test` exits 0" });

        Assert.False(report.BuildAndTestsPassed);
        var check = Assert.Single(report.Checks);
        Assert.Equal("not met", check.Status);
        Assert.Contains("1", check.Evidence);
    }

    [Fact]
    public void Execute_ValidationEscalated_CommandCheckIsCannotTell()
    {
        var report = Execute(Changed("src/A.cs"), ValidationOutcome.Escalated("npm is not installed"), new[] { "`npm test` exits 0" });

        Assert.Null(report.BuildAndTestsPassed);
        Assert.Equal("npm is not installed", report.BuildAndTestDetail);
        Assert.Equal("cannot tell", Assert.Single(report.Checks).Status);
    }

    [Fact]
    public void Execute_ValidationNotRun_CommandCheckIsCannotTell_AndNoteSaysSo()
    {
        var report = Execute(Changed("src/A.cs"), null, new[] { "`npm test` exits 0" });

        Assert.Null(report.BuildAndTestsPassed);
        Assert.Equal("cannot tell", Assert.Single(report.Checks).Status);
        Assert.Contains(report.Notes, note => note.Contains("not run"));
    }

    [Fact]
    public void Execute_FreeTextCheck_IsCannotTell()
    {
        var check = Assert.Single(Execute(Changed("src/A.cs"), Validation(0), new[] { "The retry count is configurable" }).Checks);

        Assert.Equal("cannot tell", check.Status);
        Assert.Equal("Not machine-checkable; needs human or agent review.", check.Evidence);
    }

    [Fact]
    public void Execute_NoChanges_SaysNoChanges()
    {
        var report = Execute(Changed(), null, new[] { "`dotnet test` exits 0", Confined }, new[] { "src/A.cs" });

        Assert.Empty(report.FilesChanged);
        Assert.Contains(report.Notes, note => note.StartsWith("No changed files were found"));
        Assert.All(report.Checks, check => Assert.Equal("cannot tell", check.Status));
    }

    [Fact]
    public void Execute_GitFailed_ReportsReason_AndNoFiles()
    {
        var report = Execute(new GitChangedFilesResult(false, "'C:/repo' is not a git repository.", null, Array.Empty<string>()), null);

        Assert.Empty(report.FilesChanged);
        Assert.Contains("'C:/repo' is not a git repository.", report.Notes);
    }

    [Fact]
    public void Execute_ListsAffectedTests_AndNotesWhenCodeIntelligenceIsUnavailable()
    {
        Assert.Equal("tests/ATests.cs", Assert.Single(Execute(Changed("src/A.cs"), Validation(0)).Affected).File);

        var report = Execute(Changed("src/A.cs"), Validation(0), provider: new FakeProvider { Available = false });
        Assert.Empty(report.Affected);
        Assert.Contains(report.Notes, note => note.Contains("tokensave is not installed"));
    }

    [Fact]
    public void Render_ContainsEverySection()
    {
        var text = ChangeCheckRenderer.Render(Execute(Changed("src/A.cs", "src/Other.cs"), Validation(1), new[] { Confined, "`dotnet test` exits 0" }, new[] { "src/A.cs" }));

        foreach (var heading in new[] { "## Files changed", "## Outside the expected files", "## Affected tests", "## Build and tests", "## Acceptance checks", "## Notes" })
        {
            Assert.Contains(heading, text);
        }

        Assert.Contains("- src/Other.cs", text);
        Assert.Contains("- [not met] `dotnet test` exits 0", text);
    }
}
