namespace Kernel.Registry;

public enum RegisterOutcome
{
    Success,
    RejectedDuplicate,
    RejectedMalformed
}

public sealed record RegisterResult(RegisterOutcome Outcome, RegistryEntry? Entry, string? Reason)
{
    public static RegisterResult Success(RegistryEntry entry) => new(RegisterOutcome.Success, entry, null);

    public static RegisterResult RejectedDuplicate(string reason) => new(RegisterOutcome.RejectedDuplicate, null, reason);

    public static RegisterResult RejectedMalformed(string reason) => new(RegisterOutcome.RejectedMalformed, null, reason);
}
