using Kernel.Evidence;
using Kernel.Governance;
using Kernel.Supervision;

namespace Kernel.Bootstrapping;

// Kernel-relevant configuration for Boot: the three extension points S1-S3
// already expose, wired consistently at boot time rather than introducing
// any new configuration surface.
public sealed record KernelConfiguration(
    IPolicyResolver? PolicyResolver = null,
    IResourceAdmissionGate? ResourceAdmissionGate = null,
    IEvidenceSink? EvidenceSink = null);
