using Runtime.DomainContent.DeliveryExecution;
using Runtime.Execution;
using Runtime.Integration.CodeIntelligence;
using Runtime.Integration.GitOperations;

namespace Runtime.DomainContent.ChangeCheck;

// Validation is null when the build and tests were deliberately not run.
public sealed record ChangeCheckRequest(
    RepositoryExecutionContext Repository,
    GitChangedFilesResult Changes,
    ValidationOutcome? Validation,
    IReadOnlyList<string> AcceptanceChecks,
    IReadOnlyList<string> ExpectedFiles);

// Status is exactly one of: "met", "not met", "cannot tell".
public sealed record AcceptanceCheckResult(string Check, string Status, string Evidence);

public sealed record ChangeCheckReport(
    IReadOnlyList<string> FilesChanged,
    IReadOnlyList<string> FilesOutsideExpected,
    IReadOnlyList<CodeReference> Affected,
    bool? BuildAndTestsPassed,
    string? BuildAndTestDetail,
    IReadOnlyList<AcceptanceCheckResult> Checks,
    IReadOnlyList<string> Notes);
