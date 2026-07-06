using Kernel.Evidence;
using Kernel.Registry;
using Runtime.Context;
using Runtime.Hosts.Agent;

namespace Runtime.Hosts.Skill;

// The concrete specialization for Skill invocations (I3 §13, I1 §10 case 3).
// A Skill invocation is synchronous and non-pausable — it never calls the
// Kernel's Instance Supervisor and creates no durable Instance record.
public sealed class SkillHost : ISkillInvoker
{
    private readonly IRegistry _registry;
    private readonly IReadOnlyDictionary<string, IBehavior> _behaviors;
    private readonly IEvidenceSink _evidenceSink;

    public SkillHost(IRegistry registry, IReadOnlyDictionary<string, IBehavior> behaviors, IEvidenceSink? evidenceSink = null)
    {
        _registry = registry;
        _behaviors = behaviors;
        _evidenceSink = evidenceSink ?? new InMemoryEvidenceSink();
    }

    public SkillInvocationResult Invoke(RuntimeExecutionContext callerContext, string skillId)
    {
        var query = _registry.Query(skillId);
        if (!query.Found || query.Entry!.Status != RegistryEntryStatus.Active)
        {
            return SkillInvocationResult.CapabilityGap($"Skill '{skillId}' is not a Registered, Active definition.");
        }

        if (!_behaviors.TryGetValue(skillId, out var behavior))
        {
            return SkillInvocationResult.CapabilityGap($"No Behavior is bound to Skill '{skillId}'.");
        }

        var output = behavior.Execute(callerContext);

        _evidenceSink.Record(new EvidenceRecord(
            Contract: "SkillHost",
            Decision: "Invoked",
            SubjectId: skillId,
            Reason: null,
            Timestamp: DateTimeOffset.UtcNow));

        return new SkillInvocationResult(SkillInvocationOutcome.Invoked, output, Reason: null);
    }
}
