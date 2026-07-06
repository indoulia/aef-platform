namespace Kernel.Supervision;

public sealed record AdmissionDecision(bool Allowed, string? Reason)
{
    public static AdmissionDecision Allow() => new(true, null);

    public static AdmissionDecision Deny(string reason) => new(false, reason);
}
