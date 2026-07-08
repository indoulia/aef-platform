using System.Text.RegularExpressions;

namespace Runtime.Ai.Prompts;

// Never leaves a raw {{placeholder}} in a rendered prompt sent to a real
// provider (that would silently corrupt the call) - a variable the
// caller never supplied at all is reported back as missing, same
// "escalate, never fabricate" discipline every existing Behavior in
// this Runtime follows. A variable the caller supplied as a genuine
// null (several Prompt Contracts type fields "object | null", e.g.
// specification_instance) is NOT missing - it renders as the literal
// text "null" so the AI sees an explicit absence signal instead of the
// call being escalated for a legitimate, contract-shaped input.
public static class PromptRenderer
{
    private static readonly Regex PlaceholderPattern = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    public static PromptRenderResult Render(PromptTemplate template, IReadOnlyDictionary<string, object?> variables)
    {
        var missing = new List<string>();
        var rendered = PlaceholderPattern.Replace(template.UserPromptTemplate, match =>
        {
            var key = match.Groups[1].Value;
            if (variables.TryGetValue(key, out var value))
            {
                return value?.ToString() ?? "null";
            }

            missing.Add(key);
            return match.Value;
        });

        return missing.Count > 0
            ? PromptRenderResult.MissingVariablesResult(missing)
            : PromptRenderResult.Rendered(rendered);
    }
}
