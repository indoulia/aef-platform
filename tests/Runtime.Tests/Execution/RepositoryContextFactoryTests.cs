using System.Diagnostics;
using Runtime.Execution;

namespace Runtime.Tests.Execution;

public class RepositoryContextFactoryTests
{
    [Fact]
    public void Create_RealDotnetGitRepository_ProducesFullyPopulatedContext()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            RunGit(root, "init", "-b", "main");
            RunGit(root, "config", "user.email", "test@example.com");
            RunGit(root, "config", "user.name", "Test User");
            File.WriteAllText(Path.Combine(root, "app.csproj"), "<Project />");
            RunGit(root, "add", "-A");
            RunGit(root, "commit", "-m", "initial");

            var context = RepositoryContextFactory.Create("test-repo", root);

            Assert.True(context.IsReadable);
            Assert.True(context.IsGitRepository);
            Assert.Equal("main", context.CurrentBranch);
            Assert.True(context.IsWorkingTreeClean);
            Assert.NotNull(context.TechnologyProfile);
            Assert.Contains(context.TechnologyProfile!.Technologies, t => t.Name == "dotnet");
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void Create_DirtyWorkingTree_PopulatesChangedPathsFromGitStatus()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            RunGit(root, "init", "-b", "main");
            RunGit(root, "config", "user.email", "test@example.com");
            RunGit(root, "config", "user.name", "Test User");
            File.WriteAllText(Path.Combine(root, "app.csproj"), "<Project />");
            RunGit(root, "add", "-A");
            RunGit(root, "commit", "-m", "initial");
            File.WriteAllText(Path.Combine(root, "new-file.txt"), "content");

            var context = RepositoryContextFactory.Create("test-repo", root);

            Assert.False(context.IsWorkingTreeClean);
            Assert.NotNull(context.ChangedPaths);
            Assert.Contains("new-file.txt", context.ChangedPaths!);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void Create_NonExistentRoot_ReportsUnreadableWithNoTechnologyProfile()
    {
        var context = RepositoryContextFactory.Create("missing-repo", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        Assert.False(context.IsReadable);
        Assert.Null(context.TechnologyProfile);
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

        Directory.Delete(root, true);
    }
}
