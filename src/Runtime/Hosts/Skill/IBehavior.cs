using Runtime.Context;

namespace Runtime.Hosts.Skill;

// One declared Behavior sequence (I3 §13) a registered Skill binds to.
public interface IBehavior
{
    object? Execute(RuntimeExecutionContext context);
}
