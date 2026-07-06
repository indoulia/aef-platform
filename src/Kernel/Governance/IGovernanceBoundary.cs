namespace Kernel.Governance;

// The Kernel-boundary shape of I4 §8's Activation Contract — the gate S3
// (Instance Supervisor, not yet built) must clear before creating any
// Runtime Instance. Public because S3 calls it to function.
public interface IGovernanceBoundary
{
    ActivationResult CheckActivation(ActivationRequest request);
}
