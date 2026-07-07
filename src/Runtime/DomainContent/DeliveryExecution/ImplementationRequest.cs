namespace Runtime.DomainContent.DeliveryExecution;

// What a Story/Feature/Bug's own Plan claims was implemented - inert
// input data only. No member of this record can cause a merge, push, or
// other integration-line mutation; verifying it against reality is
// IIsolatedChangeVerifier's job, never this record's own.
public sealed record ImplementationRequest(
    string StoryReferenceId,
    string BranchName,
    IReadOnlyList<string> CommitShas,
    string PlanSummary);
