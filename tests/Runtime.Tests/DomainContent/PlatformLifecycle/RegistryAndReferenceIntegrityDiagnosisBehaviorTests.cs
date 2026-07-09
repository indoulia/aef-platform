using Runtime.Context;
using Runtime.DomainContent.PlatformLifecycle;
using Runtime.Execution;

namespace Runtime.Tests.DomainContent.PlatformLifecycle;

public class RegistryAndReferenceIntegrityDiagnosisBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(RegistryAndReferenceIntegrityDiagnosisRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["RegistryAndReferenceIntegrityDiagnosisRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "PlatformLifecycle-Repair", content);
    }

    private static string CreateTempRepoWithDocs(params string[] fileContents)
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        for (var i = 0; i < fileContents.Length; i++)
        {
            File.WriteAllText(Path.Combine(root, $"doc-{i}.md"), fileContents[i]);
        }
        return root;
    }

    [Fact]
    public void Execute_NoRequestSupplied_ReturnsNoFindingsWithOpenItem()
    {
        var behavior = new RegistryAndReferenceIntegrityDiagnosisBehavior();

        var result = (RegistryAndReferenceIntegrityDiagnosisResult)behavior.Execute(ContextWith(null))!;

        Assert.Empty(result.Findings);
        Assert.Contains(result.OpenItems, i => i.Contains("no registry and reference integrity diagnosis request", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Execute_AllReferencesResolve_ClassifiesPass()
    {
        var root = CreateTempRepoWithDocs(
            "Id: ADR-0001\nDepends On: []\nUsed By: []\nSupersedes: []\n",
            "Id: ADR-0002\nDepends On: [ADR-0001]\nUsed By: []\nSupersedes: []\n");
        try
        {
            var repoContext = new RepositoryExecutionContext(root, "test-repo", true, false, null, null, null);
            var behavior = new RegistryAndReferenceIntegrityDiagnosisBehavior();

            var result = (RegistryAndReferenceIntegrityDiagnosisResult)behavior.Execute(
                ContextWith(new RegistryAndReferenceIntegrityDiagnosisRequest(repoContext)))!;

            Assert.NotEmpty(result.Findings);
            Assert.All(result.Findings, f => Assert.Equal("PASS", f.Classification));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Execute_UnresolvedReference_ClassifiesFail_CitedByIdAndFile()
    {
        var root = CreateTempRepoWithDocs(
            "Id: ADR-0002\nDepends On: [ADR-9999]\nUsed By: []\nSupersedes: []\n");
        try
        {
            var repoContext = new RepositoryExecutionContext(root, "test-repo", true, false, null, null, null);
            var behavior = new RegistryAndReferenceIntegrityDiagnosisBehavior();

            var result = (RegistryAndReferenceIntegrityDiagnosisResult)behavior.Execute(
                ContextWith(new RegistryAndReferenceIntegrityDiagnosisRequest(repoContext)))!;

            var failure = Assert.Single(result.Findings, f => f.Classification == "FAIL");
            Assert.Contains("ADR-9999", failure.Citation);
            Assert.Contains("doc-0.md", failure.Citation);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Execute_PathStyleReference_ResolvedAgainstFileExistence()
    {
        var root = CreateTempRepoWithDocs("Id: ADR-0003\nDepends On: [doc-0.md]\nUsed By: []\nSupersedes: []\n");
        try
        {
            var repoContext = new RepositoryExecutionContext(root, "test-repo", true, false, null, null, null);
            var behavior = new RegistryAndReferenceIntegrityDiagnosisBehavior();

            var result = (RegistryAndReferenceIntegrityDiagnosisResult)behavior.Execute(
                ContextWith(new RegistryAndReferenceIntegrityDiagnosisRequest(repoContext)))!;

            Assert.Contains(result.Findings, f => f.Citation.Contains("doc-0.md") && f.Classification == "PASS");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Execute_AbsolutePathToken_NeverEscapesRoot_ClassifiesFail()
    {
        var outsideFile = Path.GetTempFileName();
        var root = CreateTempRepoWithDocs($"Id: ADR-0004\nDepends On: [{outsideFile.Replace("\\", "/")}]\nUsed By: []\nSupersedes: []\n");
        try
        {
            var repoContext = new RepositoryExecutionContext(root, "test-repo", true, false, null, null, null);
            var behavior = new RegistryAndReferenceIntegrityDiagnosisBehavior();

            var result = (RegistryAndReferenceIntegrityDiagnosisResult)behavior.Execute(
                ContextWith(new RegistryAndReferenceIntegrityDiagnosisRequest(repoContext)))!;

            Assert.Contains(result.Findings, f => f.Classification == "FAIL");
        }
        finally
        {
            Directory.Delete(root, true);
            File.Delete(outsideFile);
        }
    }

    [Fact]
    public void Execute_TraversalToken_NeverEscapesRoot_ClassifiesFail()
    {
        var root = CreateTempRepoWithDocs("Id: ADR-0005\nDepends On: [../../../../Windows/System32/drivers/etc/hosts]\nUsed By: []\nSupersedes: []\n");
        try
        {
            var repoContext = new RepositoryExecutionContext(root, "test-repo", true, false, null, null, null);
            var behavior = new RegistryAndReferenceIntegrityDiagnosisBehavior();

            var result = (RegistryAndReferenceIntegrityDiagnosisResult)behavior.Execute(
                ContextWith(new RegistryAndReferenceIntegrityDiagnosisRequest(repoContext)))!;

            Assert.Contains(result.Findings, f => f.Classification == "FAIL");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Execute_NoMarkdownFiles_ReturnsNoFindings_NeverFabricated()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var repoContext = new RepositoryExecutionContext(root, "test-repo", true, false, null, null, null);
            var behavior = new RegistryAndReferenceIntegrityDiagnosisBehavior();

            var result = (RegistryAndReferenceIntegrityDiagnosisResult)behavior.Execute(
                ContextWith(new RegistryAndReferenceIntegrityDiagnosisRequest(repoContext)))!;

            Assert.Empty(result.Findings);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Execute_AlwaysNotesRegistryRegistrationLimitation()
    {
        var root = CreateTempRepoWithDocs("Id: ADR-0001\nDepends On: []\nUsed By: []\nSupersedes: []\n");
        try
        {
            var repoContext = new RepositoryExecutionContext(root, "test-repo", true, false, null, null, null);
            var behavior = new RegistryAndReferenceIntegrityDiagnosisBehavior();

            var result = (RegistryAndReferenceIntegrityDiagnosisResult)behavior.Execute(
                ContextWith(new RegistryAndReferenceIntegrityDiagnosisRequest(repoContext)))!;

            Assert.Contains(result.OpenItems, i => i.Contains("Runtime Registry", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
