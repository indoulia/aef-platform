namespace Runtime.Context;

// The Runtime's default, conservative disposal policy today, absent any
// ARB ruling on AI Memory Evolution (I1 §7).
public sealed class DiscardUnconditionallyPolicy : IContextDisposalPolicy
{
    public bool ShouldDiscardOnCompletion(string instanceId) => true;
}
