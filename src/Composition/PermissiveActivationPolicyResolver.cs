// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
using Kernel.Governance;

namespace AefPlatform.Host;

public sealed class PermissiveActivationPolicyResolver : IPolicyResolver
{
    public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
}
