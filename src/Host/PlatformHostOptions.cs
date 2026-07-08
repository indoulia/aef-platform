using System.ComponentModel.DataAnnotations;

namespace AefPlatform.Host;

public sealed class PlatformHostOptions
{
    [Required(AllowEmptyStrings = false)]
    public string PlatformName { get; set; } = string.Empty;

    public bool FailFastOnRegistrationError { get; set; } = true;
}
