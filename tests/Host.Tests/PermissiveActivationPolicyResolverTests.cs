using AefPlatform.Host;
using Kernel.Governance;

namespace Host.Tests;

public class PermissiveActivationPolicyResolverTests
{
    [Fact]
    public void Resolve_AnyRequest_Authorizes()
    {
        var resolver = new PermissiveActivationPolicyResolver();

        var result = resolver.Resolve(new ActivationRequest("any-id"));

        Assert.True(result.Authorized);
    }
}
