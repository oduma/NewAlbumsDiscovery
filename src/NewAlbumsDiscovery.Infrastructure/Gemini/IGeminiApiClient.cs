namespace NewAlbumsDiscovery.Infrastructure.Gemini;

/// <summary>
/// Thin seam around the Mscc.GenerativeAI SDK call itself — prompt string in, raw response text
/// out. This is the genuinely untestable edge (real network call to a paid external API), exempted
/// from the branch-coverage mandate the same way AppDbContext/LovedTrackDbContext were in Phase 3.
/// GeminiDiscoveryClient's template selection/rendering/retry/parsing logic, which consumes this
/// interface, is ordinary mockable control flow and IS held to normal coverage expectations.
/// </summary>
public interface IGeminiApiClient
{
    Task<string> GenerateContentAsync(string prompt, CancellationToken cancellationToken);
}
