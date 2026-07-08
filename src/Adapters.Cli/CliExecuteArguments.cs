namespace Adapters.Cli;

public sealed record CliExecuteArguments(string UserRequest, string ProjectProfileId, string? SessionId, string? PayloadJson, string Output);
