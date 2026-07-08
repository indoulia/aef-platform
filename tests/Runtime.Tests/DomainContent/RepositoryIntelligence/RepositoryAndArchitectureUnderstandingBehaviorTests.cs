using Runtime.Context;
using Runtime.DomainContent.RepositoryIntelligence;
using Runtime.Execution;

namespace Runtime.Tests.DomainContent.RepositoryIntelligence;

public class RepositoryAndArchitectureUnderstandingBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(RepositoryUnderstandingRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["RepositoryUnderstandingRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "RepoIntel-Understanding", content);
    }

    [Fact]
    public void Execute_NoRequestSupplied_ReturnsUnknownFinding()
    {
        var behavior = new RepositoryAndArchitectureUnderstandingBehavior();

        var result = (RepositoryUnderstandingResult)behavior.Execute(ContextWith(null))!;

        Assert.Equal(FindingClass.Unknown, result.Findings[0].Class);
    }

    [Fact]
    public void Execute_UnreadableRepository_ReturnsUnknownFinding()
    {
        var behavior = new RepositoryAndArchitectureUnderstandingBehavior();
        var repoContext = new RepositoryExecutionContext(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), "missing-repo", false, false, null, null, null);

        var result = (RepositoryUnderstandingResult)behavior.Execute(ContextWith(new RepositoryUnderstandingRequest("src", repoContext)))!;

        Assert.Equal(FindingClass.Unknown, result.Findings[0].Class);
    }

    [Fact]
    public void Execute_MatchingTopLevelDirectory_ReturnsPassFinding()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src"));
            var repoContext = new RepositoryExecutionContext(root, "test-repo", true, false, null, null, null);
            var behavior = new RepositoryAndArchitectureUnderstandingBehavior();

            var result = (RepositoryUnderstandingResult)behavior.Execute(ContextWith(new RepositoryUnderstandingRequest("src", repoContext)))!;

            Assert.Equal(FindingClass.Pass, result.Findings[0].Class);
            Assert.Contains("src", result.Answer);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Execute_EmptyRequestScope_ReturnsUnknownFinding_NeverFabricatesAMatch()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            var repoContext = new RepositoryExecutionContext(root, "test-repo", true, false, null, null, null);
            var behavior = new RepositoryAndArchitectureUnderstandingBehavior();

            var result = (RepositoryUnderstandingResult)behavior.Execute(ContextWith(new RepositoryUnderstandingRequest("", repoContext)))!;

            Assert.Equal(FindingClass.Unknown, result.Findings[0].Class);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Execute_NoMatch_ReturnsUnknownFinding()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var repoContext = new RepositoryExecutionContext(root, "test-repo", true, false, null, null, null);
            var behavior = new RepositoryAndArchitectureUnderstandingBehavior();

            var result = (RepositoryUnderstandingResult)behavior.Execute(ContextWith(new RepositoryUnderstandingRequest("nonexistent-module", repoContext)))!;

            Assert.Equal(FindingClass.Unknown, result.Findings[0].Class);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
