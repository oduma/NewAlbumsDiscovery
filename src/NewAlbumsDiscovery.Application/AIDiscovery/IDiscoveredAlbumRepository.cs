using NewAlbumsDiscovery.Domain.AIDiscovery;

namespace NewAlbumsDiscovery.Application.AIDiscovery;

/// <summary>
/// Writes to and reads from the internal, read-write new-albums-discovery.db. Unlike
/// IAggregatedBucketRepository.ReplaceAllAsync, AddRangeAsync is additive — discovered albums
/// accumulate over time and a run must never wipe previously discovered ones.
/// </summary>
public interface IDiscoveredAlbumRepository
{
    Task<IReadOnlyList<DiscoveredAlbum>> GetAllAsync(CancellationToken cancellationToken);

    Task AddRangeAsync(IReadOnlyList<DiscoveredAlbum> albums, CancellationToken cancellationToken);
}
