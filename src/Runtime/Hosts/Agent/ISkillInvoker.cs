using Runtime.Context;

namespace Runtime.Hosts.Agent;

// The abstraction the Agent Host's own exposed "invoke-a-declared-Skill"
// call (I3 §12) delegates to. The Skill Host (S7, Construction Unit 13)
// is this abstraction's real implementation; until one is configured, an
// invocation is a capability gap (I3 §12's own "signal-a-capability-gap").
public interface ISkillInvoker
{
    SkillInvocationResult Invoke(RuntimeExecutionContext callerContext, string skillId);
}
