namespace Runtime.DomainContent.Onboarding;

// A Draft Technology Profile (TPR), per docs/discovery/composition/
// 04-project-onboarding.md §2 Step 2. Status is always "Draft" - this type
// has no member capable of representing any other value, so Registration
// cannot happen through it (Story S20's own step).
public sealed record TechnologyProfileDraft(string Id, string Stack)
{
    public string Status => "Draft";
}
