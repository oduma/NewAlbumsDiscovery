using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.MusicAggregator;

/// <summary>
/// Writes to the internal, read-write new-albums-discovery.db. Implementations must replace the
/// entire AggregatedBuckets table atomically (docs/requirements/phase3-requirements.md §6) — a
/// zero-downtime DELETE + INSERT within a single transaction, never a partial write.
/// </summary>
public interface IAggregatedBucketRepository
{
    Task ReplaceAllAsync(IReadOnlyList<AggregatedBucket> buckets, CancellationToken cancellationToken);
}
