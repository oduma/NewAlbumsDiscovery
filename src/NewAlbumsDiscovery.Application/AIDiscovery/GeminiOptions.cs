namespace NewAlbumsDiscovery.Application.AIDiscovery;

/// <summary>
/// Bound from configuration section "NewAlbumsDiscovery:Gemini" (docs/requirements/FUNCTIONAL_REQUIREMENTS.md
/// → Phase 4). The API key itself is deliberately NOT a property here — it's read directly from
/// the flat "NewAlbumsDiscovery:GeminiApiKey" config path in AddInfrastructureServices, the same
/// way LovedTracksDbPath/AppDbPath are, since it lives outside this nested section.
/// </summary>
public sealed class GeminiOptions
{
    public string Model { get; set; } = "gemini-1.5-flash";
    public int MaxAlbumsPerPrompt { get; set; } = 20;
    public string Timeframe { get; set; } = "last 30 days";
    public int MaxRetryAttempts { get; set; } = 3;
    public int InitialBackoffSeconds { get; set; } = 2;
}
