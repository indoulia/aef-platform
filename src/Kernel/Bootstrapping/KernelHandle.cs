using Kernel.Governance;
using Kernel.Registry;
using Kernel.Supervision;

namespace Kernel.Bootstrapping;

// Exposes all three of the Kernel's public contracts (I4 §8) to a test harness,
// per this Story's own acceptance criterion.
public sealed record KernelHandle(IRegistry Registry, IGovernanceBoundary GovernanceBoundary, IInstanceSupervisor Supervisor);
