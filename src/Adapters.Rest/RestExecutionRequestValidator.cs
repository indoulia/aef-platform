namespace Adapters.Rest;

public static class RestExecutionRequestValidator
{
    public static string? Validate(RestExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserRequest))
        {
            return "UserRequest must not be empty.";
        }

        if (string.IsNullOrWhiteSpace(request.ProjectProfileId))
        {
            return "ProjectProfileId must not be empty.";
        }

        return null;
    }
}
