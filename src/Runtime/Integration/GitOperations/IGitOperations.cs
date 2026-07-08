namespace Runtime.Integration.GitOperations;

// The Git Operations Integration Adapter Boundary contract (P3 §3) - a
// broader surface than IIsolatedChangeVerifier's own deliberately narrow
// verification-only scope (Story S30). No member accepts a network
// address or credential; every implementation must invoke git via
// ProcessStartInfo.ArgumentList only.
public interface IGitOperations
{
    bool IsGitRepository();

    GitStatusResult GetStatus();

    string GetDiff();

    GitOperationResult CreateBranch(string branchName);

    GitOperationResult Commit(string message);

    MergeReadinessResult CheckMergeReadiness(string branchName, string defaultBranchName);
}
