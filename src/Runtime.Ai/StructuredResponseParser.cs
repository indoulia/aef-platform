using System.Text.Json;
using System.Text.RegularExpressions;

namespace Runtime.Ai;

// Hand-rolled, matching the repo's existing no-third-party-library
// convention (no JSON-schema package referenced anywhere). "Schema
// validation" here means: valid JSON, deserializes into the caller's
// strongly-typed record (System.Text.Json already enforces the shape
// for a `record`) - never a generic untyped schema engine.
public static class StructuredResponseParser
{
    private static readonly Regex CodeFencePattern = new(@"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static bool TryParse<T>(string rawText, out T? value, out IReadOnlyList<string> errors) where T : class
    {
        var fenceMatch = CodeFencePattern.Match(rawText);
        var candidate = fenceMatch.Success ? fenceMatch.Groups[1].Value : rawText;

        try
        {
            value = JsonSerializer.Deserialize<T>(candidate, Options);
            if (value is null)
            {
                errors = new[] { "Response parsed to a null value." };
                return false;
            }

            errors = Array.Empty<string>();
            return true;
        }
        catch (JsonException ex)
        {
            value = null;
            errors = new[] { $"Response was not valid JSON: {ex.Message}" };
            return false;
        }
    }
}
