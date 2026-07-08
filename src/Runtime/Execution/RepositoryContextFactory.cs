using Runtime.DomainContent.Onboarding;
using Runtime.Integration.GitOperations;
using Runtime.Integration.RepositoryScanning;

namespace Runtime.Execution;

// Composes the existing S22/S23 scanner+detector with the new Git
// Operations adapter (Task 2) into one RepositoryExecutionContext -
// mirrors RepositoryOnboardingAssessmentAgent's own established
// composition pattern (never re-implements StackDetector's reasoning).
public static class RepositoryContextFactory
{
    public static RepositoryExecutionContext Create(string repositoryName, string repositoryRoot)
    {
        var scanner = new LocalFileSystemRepositoryScanner(repositoryRoot);
        var isReadable = scanner.IsRootReadable();

        TechnologyProfileDraft? technologyProfile = null;
        if (isReadable)
        {
            var detection = StackDetector.DetectTechnologies(scanner);
            technologyProfile = new TechnologyProfileDraft($"TPR-CTX-{Guid.NewGuid():N}"[..16], detection.Technologies);
        }

        var gitOperations = new LocalGitOperations(repositoryRoot);
        var isGitRepository = isReadable && gitOperations.IsGitRepository();

        string? currentBranch = null;
        bool? isWorkingTreeClean = null;
        if (isGitRepository)
        {
            var status = gitOperations.GetStatus();
            currentBranch = status.CurrentBranch;
            isWorkingTreeClean = status.IsClean;
        }

        return new RepositoryExecutionContext(repositoryRoot, repositoryName, isReadable, isGitRepository, currentBranch, isWorkingTreeClean, technologyProfile);
    }
}
