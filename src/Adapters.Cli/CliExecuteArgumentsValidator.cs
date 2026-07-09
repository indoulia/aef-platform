using System.Text.Json;

namespace Adapters.Cli;

public static class CliExecuteArgumentsValidator
{
    public static string? Validate(CliExecuteArguments arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments.UserRequest))
        {
            return "--user-request must not be empty.";
        }

        if (string.IsNullOrWhiteSpace(arguments.ProjectProfileId))
        {
            return "--project-profile-id must not be empty.";
        }

        if (arguments.Output is not ("text" or "json"))
        {
            return "--output must be 'text' or 'json'.";
        }

        if (arguments.PayloadJson is not null)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(arguments.PayloadJson);
                if (parsed is null)
                {
                    return "--payload-json must not be a JSON null.";
                }
            }
            catch (JsonException)
            {
                return "--payload-json must be valid JSON representing an object.";
            }
        }

        return null;
    }
}
