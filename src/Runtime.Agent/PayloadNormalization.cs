using System.Text.Json;

namespace Runtime.Agent;

// Execution-discovered adapter translation defect (GA-3 Capability 5): both
// REST's [FromBody] model binding and CLI's own JsonSerializer.Deserialize
// call produce IReadOnlyDictionary<string, object> payloads whose values are
// boxed System.Text.Json.JsonElement instances, not plain CLR types.
// EngineeringExecutionPipeline.TryGetRepositoryContext's `is string name`
// pattern match never matches a JsonElement, so RepositoryName/
// RepositoryRoot (and any other payload value) silently failed to be
// recognized by every JSON-delivered request on both channels. Lives here,
// not in either adapter project, since both already depend on Runtime.Agent
// and the type being normalized (ExecutionRequest.Payload's own shape) is
// Runtime.Agent's own contract - avoids duplicating identical logic in
// Adapters.Rest and Adapters.Cli. EngineeringExecutionPipeline itself is
// unchanged: it continues to receive plain CLR types exactly as it always
// expected, restoring "adapters translate transport representations into
// Runtime representations, the Runtime is never aware of transport-specific
// serialization types."
public static class PayloadNormalization
{
    public static IReadOnlyDictionary<string, object>? Normalize(IReadOnlyDictionary<string, object>? payload)
    {
        if (payload is null)
        {
            return null;
        }

        var normalized = new Dictionary<string, object>();
        foreach (var (key, value) in payload)
        {
            normalized[key] = NormalizeValue(value);
        }

        return normalized;
    }

    private static object NormalizeValue(object value)
    {
        if (value is not JsonElement element)
        {
            return value;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Array => element.EnumerateArray().Select(e => NormalizeValue(e)).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => NormalizeValue(p.Value)),
            _ => value,
        };
    }
}
