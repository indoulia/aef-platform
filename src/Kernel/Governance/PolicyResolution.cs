namespace Kernel.Governance;

public sealed record PolicyResolution(bool Authorized, string? Reason)
{
    public static PolicyResolution Authorize() => new(true, null);

    public static PolicyResolution Deny(string reason) => new(false, reason);
}
