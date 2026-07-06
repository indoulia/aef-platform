namespace Runtime.Context;

// The single, Policy-declared disposal parameter I1 §7 requires: a future
// ARB ruling on AI Memory Evolution is adopted by supplying a different
// implementation here, never by redesigning the Execution Context Manager.
public interface IContextDisposalPolicy
{
    bool ShouldDiscardOnCompletion(string instanceId);
}
