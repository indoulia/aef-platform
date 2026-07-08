namespace Runtime.Integration.GitOperations;

// The working-tree status Repository Context needs (P3 §1). IsClean is
// true only when neither staged, unstaged, nor untracked entries exist.
public sealed record GitStatusResult(
    string? CurrentBranch,
    bool IsClean,
    IReadOnlyList<string> ModifiedPaths,
    IReadOnlyList<string> UntrackedPaths);
