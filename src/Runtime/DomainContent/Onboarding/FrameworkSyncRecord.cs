namespace Runtime.DomainContent.Onboarding;

// Records the one pinned AEF version framework content was synced at, per
// docs/discovery/composition/04-project-onboarding.md §2 Step 6.
public sealed record FrameworkSyncRecord(string PinnedVersion);
