namespace Adapters.Cli;

public static class CliInstallArgumentsValidator
{
    public static string? Validate(CliInstallArguments arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments.Version))
        {
            return "--version must not be empty.";
        }

        if (string.IsNullOrWhiteSpace(arguments.TargetPath))
        {
            return "--target-path must not be empty.";
        }

        return null;
    }
}
