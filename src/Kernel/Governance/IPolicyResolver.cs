namespace Kernel.Governance;

// The pluggable Policy-resolution extension point — identical dependency-inversion
// shape to the Kernel's Resource Manager admission-gate callback (AD-72) and
// Evidence-sink (epics/decisions/0001-...): a Service registers itself against
// this Kernel-defined contract later; the Kernel never holds a hard dependency
// on a Policy engine that does not yet exist. Absent a registered resolver,
// GovernanceBoundary fails closed by default.
public interface IPolicyResolver
{
    PolicyResolution Resolve(ActivationRequest request);
}
