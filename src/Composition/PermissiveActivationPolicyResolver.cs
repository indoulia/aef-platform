// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
//
// GovernanceBoundary.CheckActivation only calls IPolicyResolver.Resolve after
// it has already confirmed the ActivationRequest's DefinitionId is
// Registered+Active — no other Policy engine exists anywhere in the
// architecture yet, so this resolver adds no restriction beyond that; it
// exists solely to opt out of the Kernel's deliberate "no resolver
// configured" fail-closed default (Kernel.Governance.IPolicyResolver's own
// doc comment). Promotes the AllowAllPolicyResolver test double already
// used throughout Kernel.Tests into a real, named production class.
using Kernel.Governance;

namespace AefPlatform.Host;

public sealed class PermissiveActivationPolicyResolver : IPolicyResolver
{
    public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
}
