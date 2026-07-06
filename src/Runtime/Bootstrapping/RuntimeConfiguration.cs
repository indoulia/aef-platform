using Kernel.Evidence;
using Kernel.Governance;
using Kernel.Supervision;
using Runtime.Catalog;
using Runtime.Context;

namespace Runtime.Bootstrapping;

// Runtime-relevant configuration for Boot: the Kernel's own three extension
// points (Epic 0001), plus the two this Epic adds — the Execution Context
// Manager's disposal policy (S5) and the Catalog's optional Contract-
// conformance provider (S9) — wired consistently at boot time.
public sealed record RuntimeConfiguration(
    IPolicyResolver? PolicyResolver = null,
    IResourceAdmissionGate? ResourceAdmissionGate = null,
    IEvidenceSink? EvidenceSink = null,
    IContextDisposalPolicy? DisposalPolicy = null,
    IContractConformanceProvider? ConformanceProvider = null);
