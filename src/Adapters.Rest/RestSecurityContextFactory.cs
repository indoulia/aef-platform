using Microsoft.AspNetCore.Http;
using Runtime.Agent.Security;

namespace Adapters.Rest;

public static class RestSecurityContextFactory
{
    public static SecurityContext FromHttpContext(HttpContext httpContext) =>
        new(PrincipalId: httpContext.User.Identity?.Name, Claims: null);
}
