using System.Linq;
using Kernel.Governance;
using Kernel.Registry;
using Runtime.Bootstrapping;
using Runtime.DomainContent.EpicLifecycle;
using Xunit;

namespace Runtime.Tests.DomainContent.EpicLifecycle;

// S16 (epics/0004-epic-granularity-autonomous-lifecycle.md) — the Next-
// Eligible-Item Discovery Skill's Behavior. All four acceptance criteria are
// tested directly against a synthetic candidate table, per the same
// synthetic-fixture-before-real-content convention Epic 0002 established for
// the Walking Skeleton (epics/decisions/0004-walking-skeleton-trivial-content-selection.md).
public class NextEligibleItemDiscoveryBehaviorTests
{
    private static RuntimeHandle BootWithRegistered(params string[] activeIds)
    {
        var runtime = RuntimeBootstrapper.Boot(new RuntimeConfiguration(PolicyResolver: new AllowAllPolicyResolver()));
        foreach (var id in activeIds)
        {
            runtime.Kernel.Registry.Register(new DefinitionSubmission(id, "Execution"));
        }

        return runtime;
    }

    [Fact]
    public void ExactlyOneCandidateWithSatisfiedDependency_ReturnsThatCandidate()
    {
        var runtime = BootWithRegistered("CU-DEP-SATISFIED");
        var candidates = new[]
        {
            new CandidateItem("CU-X", "Candidate X", "CU-DEP-SATISFIED"),
            new CandidateItem("CU-Y", "Candidate Y", "CU-DEP-UNSATISFIED"),
        };
        var behavior = new NextEligibleItemDiscoveryBehavior(runtime.Catalog, candidates);

        var outcome = (DiscoveryOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(DiscoveryStatus.Eligible, outcome.Status);
        Assert.Equal("CU-X", outcome.Candidate!.Id);
        Assert.Null(outcome.AmbiguousCandidates);
    }

    [Fact]
    public void ZeroCandidatesWithSatisfiedDependency_ReturnsNoneEligible_NeverAGuess()
    {
        var runtime = BootWithRegistered(); // nothing registered
        var candidates = new[]
        {
            new CandidateItem("CU-X", "Candidate X", "CU-DEP-UNSATISFIED-1"),
            new CandidateItem("CU-Y", "Candidate Y", "CU-DEP-UNSATISFIED-2"),
        };
        var behavior = new NextEligibleItemDiscoveryBehavior(runtime.Catalog, candidates);

        var outcome = (DiscoveryOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(DiscoveryStatus.NoneEligible, outcome.Status);
        Assert.Null(outcome.Candidate);
        Assert.Null(outcome.AmbiguousCandidates);
    }

    [Fact]
    public void MoreThanOneCandidateWithSatisfiedDependency_ReturnsAmbiguous_NeverASilentPick()
    {
        var runtime = BootWithRegistered("CU-1-MARKER");
        var candidates = new[]
        {
            new CandidateItem("CU-3", "Repository Discovery & Canonical Onboarding", "CU-1-MARKER"),
            new CandidateItem("CU-4", "Knowledge & Governance Substrate", "CU-1-MARKER"),
        };
        var behavior = new NextEligibleItemDiscoveryBehavior(runtime.Catalog, candidates);

        var outcome = (DiscoveryOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(DiscoveryStatus.Ambiguous, outcome.Status);
        Assert.Null(outcome.Candidate);
        Assert.Equal(2, outcome.AmbiguousCandidates!.Count);
        Assert.Contains(outcome.AmbiguousCandidates, c => c.Id == "CU-3");
        Assert.Contains(outcome.AmbiguousCandidates, c => c.Id == "CU-4");
    }

    [Fact]
    public void NeverWritesANewCandidateRow_EnforcedByType()
    {
        // Structural guarantee, not a runtime behavior: the Behavior's only
        // reference to the candidate table is IReadOnlyList<CandidateItem>,
        // which exposes no member capable of adding, removing, or editing a
        // row. This test documents that guarantee by reflection so a future
        // signature change that widens it to a mutable collection fails loudly.
        var candidatesParameter = typeof(NextEligibleItemDiscoveryBehavior)
            .GetConstructors()[0]
            .GetParameters()
            .Single(p => p.Name == "candidates");

        Assert.Equal(typeof(IReadOnlyList<CandidateItem>), candidatesParameter.ParameterType);
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }
}
