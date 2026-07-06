namespace Runtime.DomainContent.Onboarding;

// .ai-workspace/'s six categories, per docs/discovery/composition/
// 04-project-onboarding.md §2 Step 5. Fixed - the workspace shape is the
// same for every project, not configurable per registration.
public static class WorkspaceInitialization
{
    public static readonly IReadOnlyList<string> Categories = new[]
    {
        "discovery", "investigations", "reviews", "reports", "sessions", "generated",
    };
}
