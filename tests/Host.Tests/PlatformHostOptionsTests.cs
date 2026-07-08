using System.ComponentModel.DataAnnotations;
using AefPlatform.Host;

namespace Host.Tests;

public class PlatformHostOptionsTests
{
    [Fact]
    public void Defaults_FailFastOnRegistrationError_IsTrue()
    {
        var options = new PlatformHostOptions { PlatformName = "AEF Platform Runtime" };

        Assert.True(options.FailFastOnRegistrationError);
    }

    [Fact]
    public void Validation_MissingPlatformName_FailsValidation()
    {
        var options = new PlatformHostOptions { PlatformName = "" };
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(PlatformHostOptions.PlatformName)));
    }

    [Fact]
    public void Validation_PlatformNameProvided_Passes()
    {
        var options = new PlatformHostOptions { PlatformName = "AEF Platform Runtime" };
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        Assert.True(isValid);
    }
}
