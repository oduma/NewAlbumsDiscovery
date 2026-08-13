namespace NewAlbumsDiscovery.Application.AIDiscovery;

/// <summary>
/// A candidate album as parsed from a Gemini prompt response, before dedup filtering.
/// </summary>
public sealed record CandidateAlbum(string Artist, string Album);

/// <summary>
/// Parameters for a single bucket's discovery prompt. Language/Genre being null drives which of
/// the three prompt templates (country / country-language / country-language-genre) gets used —
/// see docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 4 Design Notes.
/// </summary>
public sealed record DiscoveryPromptRequest(string Country, string? Language, string? Genre, int MaxAlbums, string Timeframe);

/// <summary>
/// Infrastructure Isolation boundary per docs/constitution/tdd.md §3 — the Gemini API is
/// abstracted behind this Application-layer port so DiscoverAlbumsCommandHandler can be fully
/// unit-tested with a mock, never touching the real SDK.
/// </summary>
public interface IGeminiDiscoveryClient
{
    Task<IReadOnlyList<CandidateAlbum>> DiscoverAsync(DiscoveryPromptRequest request, CancellationToken cancellationToken);
}
