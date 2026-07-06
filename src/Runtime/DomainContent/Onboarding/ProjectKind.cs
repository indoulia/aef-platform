namespace Runtime.DomainContent.Onboarding;

// The greenfield/existing-project divergence, per docs/discovery/
// composition/04-project-onboarding.md §3 - the two paths diverge at the
// baseline Quality Gate run (Step 7).
public enum ProjectKind
{
    Greenfield,
    Existing,
}
