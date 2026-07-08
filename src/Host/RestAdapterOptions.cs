using System.ComponentModel.DataAnnotations;

namespace AefPlatform.Host;

public sealed class RestAdapterOptions
{
    [Range(1, 65535)]
    public int Port { get; set; } = 5080;

    public string BasePath { get; set; } = "/api/v1";

    public long MaxRequestBodyBytes { get; set; } = 1_048_576;
}
