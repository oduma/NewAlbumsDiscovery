namespace NewAlbumsDiscovery.Application.AIDiscovery;

/// <summary>
/// Bound from configuration section "NewAlbumsDiscovery:Gemini". Trimmed to just the model name
/// after Phase 4's actual Gemini-calling code was rolled back (docs/requirements/
/// FUNCTIONAL_REQUIREMENTS.md → Phase 4 → Rollback) — kept as scaffolding for whichever future
/// phase rebuilds real Gemini calls inside Phase 5's pipeline. The API key itself is not a
/// property here — it's read directly from the flat "NewAlbumsDiscovery:GeminiApiKey" config
/// path, the same way LovedTracksDbPath/AppDbPath are, since it lives outside this nested section.
/// </summary>
public sealed class GeminiOptions
{
    public string Model { get; set; } = "gemini-1.5-flash";
}
