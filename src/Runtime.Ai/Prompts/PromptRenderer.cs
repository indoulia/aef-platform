using System.Text.RegularExpressions;

namespace Runtime.Ai.Prompts;

// Never leaves a raw {{placeholder}} in a rendered prompt sent to a real
// provider (that would silently corrupt the call) - a missing variable
// is reported back to the caller instead, same "escalate, never
// fabricate" discipline every existing Behavior in this Runtime follows.
public static class PromptRenderer
{
    private static readonly Regex PlaceholderPattern = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    public static PromptRenderResult Render(PromptTemplate template, IReadOnlyDictionary<string, object?> variables)
    {
        var missing = new List<string>();
        var rendered = PlaceholderPattern.Replace(template.UserPromptTemplate, match =>
        {
            var key = match.Groups[1].Value;
            if (variables.TryGetValue(key, out var value) && value is not null)
            {
                return value.ToString() ?? string.Empty;
            }

            missing.Add(key);
            return match.Value;
        });

        return missing.Count > 0
            ? PromptRenderResult.MissingVariablesResult(missing)
            : PromptRenderResult.Rendered(rendered);
    }
}
