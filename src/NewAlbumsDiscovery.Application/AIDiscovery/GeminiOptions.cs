namespace NewAlbumsDiscovery.Application.AIDiscovery;

/// <summary>
/// Bound from configuration section "NewAlbumsDiscovery:Gemini". The API key itself is not a
/// property here - it's read directly from the flat "NewAlbumsDiscovery:GeminiApiKey" config
/// path by the Infrastructure adapter that calls Gemini, the same way LovedTracksDbPath/AppDbPath
/// are read, since it lives outside this nested section (docs/requirements/
/// FUNCTIONAL_REQUIREMENTS.md → Phase 9).
/// </summary>
public sealed class GeminiOptions
{
    public string Model { get; set; } = "gemini-3.5-flash";

    public int[] RetryBackoffSeconds { get; set; } = [10, 30, 180];
}
