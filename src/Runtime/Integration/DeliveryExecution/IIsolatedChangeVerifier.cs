namespace Runtime.Integration.DeliveryExecution;

// The generic Integration Adapter Boundary contract for verifying an
// isolated change's real state (ai-engineering-framework repo, Epic 0005,
// Story S30) - every integration is an adapter binding, never an
// architectural dependency (docs/aep/P1-Autonomous-Engineering-Platform-
// Architecture.md §7/§11), mirroring IRepositoryScanner's own shape
// (Epic 0008, Story S22). This contract is read-only/query-only by
// design: no member may merge, push, checkout, or otherwise mutate
// anything - RUL-DEL-0004's isolation boundary (Exploratory-Work
// Isolation, CEI-DEL-08) is enforced structurally by this interface
// never exposing a capability that could lift it, not by caller
// discipline alone. Mechanically enforced by a reflection-based contract
// test (LocalGitChangeVerifierTests.Contract_...), the same technique
// Story S25 established for IRepositoryScanner.
public interface IIsolatedChangeVerifier
{
    // Whether a branch by this name exists in the repository at all.
    bool BranchExists(string branchName);

    // Whether this branch name is the repository's own default/primary
    // integration line - never inferred from a caller's own claim about
    // "isolation," always verified against the repository's real
    // configuration.
    bool IsDefaultBranch(string branchName);

    // Whether the given commit exists and is reachable from the named
    // branch's history - never a caller-supplied assertion.
    bool CommitExistsOnBranch(string branchName, string commitSha);
}
