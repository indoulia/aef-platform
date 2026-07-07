using Runtime.Integration.RepositoryScanning;

namespace Runtime.DomainContent.Onboarding;

// The assessed characteristics of a target repository, supplied to
// SKL-EXE-0015 per invocation - no real filesystem/IO Construction Unit
// exists yet for persistence, mirroring FeatureWorkspace's own "no
// persistence yet" precedent. Amended per ADR-0013 / Epic 0008 Story S28
// (ai-engineering-framework repo, docs/aep/ARB-Repository-Technology-Domain-Model.md):
// TechnologyDetection - a three-valued result - replaces the prior scalar
// PrimaryStack. Null only when IsReadable is false: an Unreadable
// repository never reaches Technology Detection at all (INV-1).
public sealed record RepositoryDescriptor(string Name, bool IsReadable, TechnologyDetectionResult? TechnologyDetection);
