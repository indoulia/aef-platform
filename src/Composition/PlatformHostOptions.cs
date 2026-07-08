// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
using System.ComponentModel.DataAnnotations;

namespace AefPlatform.Host;

public sealed class PlatformHostOptions
{
    [Required(AllowEmptyStrings = false)]
    public string PlatformName { get; set; } = string.Empty;

    public bool FailFastOnRegistrationError { get; set; } = true;
}
