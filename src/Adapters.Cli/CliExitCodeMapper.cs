using Runtime.Agent;

namespace Adapters.Cli;

public static class CliExitCodeMapper
{
    public const int UsageError = 64;
    public const int SoftwareError = 70;
    public const int SecurityDenied = 77;

    public static int MapExecutionStatus(ExecutionStatus status) => status switch
    {
        ExecutionStatus.Completed => 0,
        ExecutionStatus.NeedsClarification => 2,
        ExecutionStatus.Failed => 1,
        _ => SoftwareError,
    };
}
