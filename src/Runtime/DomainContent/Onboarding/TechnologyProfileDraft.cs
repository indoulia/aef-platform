using Runtime.Integration.RepositoryScanning;

namespace Runtime.DomainContent.Onboarding;

// A Draft Technology Profile (TPR), per docs/discovery/composition/
// 04-project-onboarding.md §2 Step 2. Status is always "Draft" - this type
// has no member capable of representing any other value, so Registration
// cannot happen through it (Story S20's own step). Amended per ADR-0013 /
// Epic 0008 Story S28: Technologies (a non-empty evidence-based inventory,
// SCH-PRJ-0002 v2.0.0) replaces the prior scalar Stack field.
public sealed record TechnologyProfileDraft(string Id, IReadOnlyList<TechnologyEvidence> Technologies)
{
    public string Status => "Draft";
}
