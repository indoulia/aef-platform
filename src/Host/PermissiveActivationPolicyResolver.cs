using Kernel.Governance;

namespace AefPlatform.Host;

// GovernanceBoundary.CheckActivation only calls IPolicyResolver.Resolve after
// it has already confirmed the ActivationRequest's DefinitionId is
// Registered+Active — no other Policy engine exists anywhere in the
// architecture yet, so this resolver adds no restriction beyond that; it
// exists solely to opt out of the Kernel's deliberate "no resolver
// configured" fail-closed default (Kernel.Governance.IPolicyResolver's own
// doc comment). Promotes the AllowAllPolicyResolver test double already
// used throughout Kernel.Tests into a real, named production class.
public sealed class PermissiveActivationPolicyResolver : IPolicyResolver
{
    public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
}
