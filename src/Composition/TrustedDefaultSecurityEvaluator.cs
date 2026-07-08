// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
using Microsoft.Extensions.Logging;
using Runtime.Agent.Security;

namespace AefPlatform.Host;

// GA-3 Capability 1's trusted-default implementation of the security seam:
// every future adapter must call ISecurityEvaluator before submitting an
// ExecutionRequest (ADR-0025 Decision 3 - adapters translate and invoke
// the seam, never their own policy logic). Real authentication/
// authorization policy is a future capability's concern; this default
// represents a trusted local environment by authorizing everything,
// mirroring PermissiveActivationPolicyResolver's own precedent for the
// Kernel's activation-policy seam. Logs once at Debug level so a local
// run's logs make it obvious no real authorization is in effect (design
// Decision 3 - standardize on the existing ILogger<T> pattern, no new
// logging abstraction).
public sealed class TrustedDefaultSecurityEvaluator : ISecurityEvaluator
{
    private readonly ILogger<TrustedDefaultSecurityEvaluator> _logger;

    public TrustedDefaultSecurityEvaluator(ILogger<TrustedDefaultSecurityEvaluator> logger)
    {
        _logger = logger;
        _logger.LogDebug("Trusted-default security evaluator active - all requests are authorized.");
    }

    public SecurityDecision Evaluate(SecurityContext context) => new(IsAllowed: true, DenialReason: null);
}
