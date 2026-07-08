namespace Adapters.Rest.Tests;

public class RestExecutionRequestValidatorTests
{
    [Fact]
    public void Validate_ValidRequest_ReturnsNull()
    {
        var request = new RestExecutionRequest(null, "start a new feature", "default-profile", null);

        Assert.Null(RestExecutionRequestValidator.Validate(request));
    }

    [Fact]
    public void Validate_EmptyUserRequest_ReturnsError()
    {
        var request = new RestExecutionRequest(null, "", "default-profile", null);

        Assert.NotNull(RestExecutionRequestValidator.Validate(request));
    }

    [Fact]
    public void Validate_WhitespaceUserRequest_ReturnsError()
    {
        var request = new RestExecutionRequest(null, "   ", "default-profile", null);

        Assert.NotNull(RestExecutionRequestValidator.Validate(request));
    }

    [Fact]
    public void Validate_EmptyProjectProfileId_ReturnsError()
    {
        var request = new RestExecutionRequest(null, "start a new feature", "", null);

        Assert.NotNull(RestExecutionRequestValidator.Validate(request));
    }
}
