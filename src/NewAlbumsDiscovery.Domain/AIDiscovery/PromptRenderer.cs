namespace NewAlbumsDiscovery.Domain.AIDiscovery;

/// <summary>
/// Pure {{token}} substitution over a prompt template string (docs/requirements/
/// FUNCTIONAL_REQUIREMENTS.md -> Phase 7). Only keys present in <paramref name="values"/> are
/// substituted; a template token with no matching key is left untouched, and an unused key is
/// simply ignored - keeps this a generic, reusable primitive rather than coupling it to any one
/// template's exact shape.
/// </summary>
public sealed class PromptRenderer
{
    public string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);

        var rendered = template;
        foreach (var (key, value) in values)
        {
            rendered = rendered.Replace($"{{{{{key}}}}}", value);
        }

        return rendered;
    }
}
