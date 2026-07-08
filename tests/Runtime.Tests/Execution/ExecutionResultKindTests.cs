using Runtime.Execution;

namespace Runtime.Tests.Execution;

public class ExecutionResultKindTests
{
    [Fact]
    public void DefaultConstructorLeavesKindUnspecified()
    {
        var result = new ExecutionResult(true, "WFL-DEL-0001", null, Array.Empty<string>(), null, TimeSpan.Zero, null, null);

        Assert.Equal(ExecutionResultKind.Unspecified, result.Kind);
    }

    [Fact]
    public void KindCanBeSetExplicitlyViaNamedArgument()
    {
        var result = new ExecutionResult(false, null, null, Array.Empty<string>(), null, TimeSpan.Zero, null, null,
            Kind: ExecutionResultKind.NeedsClarification);

        Assert.Equal(ExecutionResultKind.NeedsClarification, result.Kind);
    }
}
