namespace Runtime.DomainContent.Onboarding;

// A Draft Project Profile (PPR), per docs/discovery/composition/
// 04-project-onboarding.md §2 Step 3 and §1.2's now/deferred adoption
// convention. Status is always "Draft" - see TechnologyProfileDraft's own
// comment for why.
public sealed record ProjectProfileDraft(string Id, string Name, IReadOnlyList<string> Adopted, IReadOnlyList<string> Deferred)
{
    public string Status => "Draft";
}
