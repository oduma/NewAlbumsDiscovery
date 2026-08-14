using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// One unit of per-bucket work inside Stage 2 (<see cref="BucketProcessingStage"/>):
/// <see cref="PrintBucketStep"/>, <see cref="GenreExpansionPromptStep"/>,
/// <see cref="DiscoveryQueryPromptStep"/>, and <see cref="AlbumPersistenceStep"/>. The
/// existingAlbumKeys set is loaded once per pipeline run by BucketProcessingStage and threaded
/// through every step/bucket call so cross-bucket dedup (Phase 10) doesn't need a separate
/// run-scoped object — steps that don't need it (all but AlbumPersistenceStep) ignore it.
/// </summary>
public interface IBucketProcessingStep
{
    Task ProcessAsync(AggregatedBucket bucket, BucketProcessingState state, ISet<AlbumKey> existingAlbumKeys, CancellationToken cancellationToken);
}
