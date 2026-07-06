namespace Kernel.Supervision;

public enum DestroyOutcome
{
    Destroyed,
    NotFound
}

public sealed record DestroyResult(DestroyOutcome Outcome);
