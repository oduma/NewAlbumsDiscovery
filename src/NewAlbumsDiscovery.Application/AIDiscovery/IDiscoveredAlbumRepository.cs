using NewAlbumsDiscovery.Domain.AIDiscovery;

namespace NewAlbumsDiscovery.Application.AIDiscovery;

/// <summary>
/// Writes to the internal, read-write new-albums-discovery.db. Unlike IAggregatedBucketRepository,
/// this is additive only — AddRangeAsync never deletes or replaces existing rows
/// (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 10 → Design Notes).
/// </summary>
public interface IDiscoveredAlbumRepository
{
    Task<ISet<AlbumKey>> GetExistingAlbumKeysAsync(CancellationToken cancellationToken);

    Task AddRangeAsync(IReadOnlyList<DiscoveredAlbum> albums, CancellationToken cancellationToken);
}
