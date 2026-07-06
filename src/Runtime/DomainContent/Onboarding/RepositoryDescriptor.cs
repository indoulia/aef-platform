namespace Runtime.DomainContent.Onboarding;

// The assessed characteristics of a target repository, supplied to
// SKL-EXE-0015 per invocation - no real filesystem/IO Construction Unit
// exists yet, so this is an in-memory stand-in, mirroring FeatureWorkspace's
// own "no persistence yet" precedent.
public sealed record RepositoryDescriptor(string Name, bool IsReadable, string? PrimaryStack);
