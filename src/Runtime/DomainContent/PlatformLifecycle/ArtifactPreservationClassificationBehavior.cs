using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.PlatformLifecycle;

// The executable realization of SKL-EXE-0047. Per the Reset Model, almost
// nothing in a governed repository is actually regenerable - every one of
// the 23 governed artifact types under the frozen 7-state Lifecycle model
// is authored content, never build output. The only artifact this design's
// own investigation found to be a mechanical projection of other Governed
// content is the Runtime Catalog's own read-only projection of the Runtime
// Registry. This Behavior therefore defaults to "Governed" (never safe to
// silently discard) for any requested scope, and classifies "Derived
// Projection" only for the one narrow, named exception - it never invents
// a larger "generated artifacts" category the repository does not
// actually have.
public sealed class ArtifactPreservationClassificationBehavior : IBehavior
{
    private const string DerivedProjectionRationale = "The Runtime Catalog is a mechanical, read-only projection of the Runtime Registry - the only artifact in this repository that is safely regenerable.";
    private const string GovernedRationale = "Authored, Founder-or-delegated-governed content under the frozen 7-state Lifecycle model - never safe to silently discard, not build output.";

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("ArtifactPreservationClassificationRequest", out var raw) || raw is not ArtifactPreservationClassificationRequest request)
        {
            return new ArtifactPreservationClassificationResult("Governed", GovernedRationale);
        }

        var isRuntimeCatalog = request.Scope.Contains("runtime catalog", StringComparison.OrdinalIgnoreCase);
        return isRuntimeCatalog
            ? new ArtifactPreservationClassificationResult("Derived Projection", DerivedProjectionRationale)
            : new ArtifactPreservationClassificationResult("Governed", GovernedRationale);
    }
}
