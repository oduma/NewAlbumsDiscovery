using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.MusicAggregator;

/// <summary>
/// Reads the external, read-only loved-tracks.db. Implementations normalize raw source data
/// (null/empty CountryOrRegion, malformed LanguagesJson/GenresJson) before returning it, so every
/// <see cref="LovedTrackPreferences"/> handed back already satisfies its own constructor invariants.
/// </summary>
public interface ILovedTrackRepository
{
    Task<IReadOnlyList<LovedTrackPreferences>> GetAllAsync(CancellationToken cancellationToken);
}
