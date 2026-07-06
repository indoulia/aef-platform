using Kernel.Governance;

namespace Kernel.Supervision;

public interface IInstanceSupervisor
{
    InstanceCreationResult CreateInstance(ActivationRequest request);

    DestroyResult DestroyInstance(string instanceId);

    bool IsActive(string instanceId);
}
