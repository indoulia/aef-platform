namespace Runtime.DomainContent.RepositoryValidation;

// Maps a StackDetector-named stack to its conventional default
// build/test command (P3 §4: Build system detection). A stack absent
// from this table has no known default command - callers must treat that
// as "no verification procedure could be constructed", never guess one,
// exactly matching DeliveryValidationBehavior's own escalation
// philosophy for a null Command.
public static class BuildSystemDetector
{
    private static readonly IReadOnlyDictionary<string, (string Command, string[] Arguments)> DefaultCommands =
        new Dictionary<string, (string, string[])>(StringComparer.OrdinalIgnoreCase)
        {
            ["dotnet"] = ("dotnet", new[] { "test" }),
            ["node"] = ("npm", new[] { "test" }),
            ["python"] = ("python", new[] { "-m", "pytest" }),
            ["java"] = ("mvn", new[] { "test" }),
            ["gradle"] = ("gradlew", new[] { "test" }),
            ["rust"] = ("cargo", new[] { "test" }),
            ["go"] = ("go", new[] { "test", "./..." }),
        };

    public static (string Command, IReadOnlyList<string> Arguments)? DetectDefaultValidationCommand(IReadOnlyList<string> detectedStackNames)
    {
        foreach (var stack in detectedStackNames)
        {
            if (DefaultCommands.TryGetValue(stack, out var command))
            {
                return (command.Command, command.Arguments);
            }
        }

        return null;
    }
}
