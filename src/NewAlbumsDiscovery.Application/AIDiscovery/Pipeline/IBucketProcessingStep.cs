using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// One unit of per-bucket work inside Stage 2 (<see cref="BucketProcessingStage"/>). Today only
/// <see cref="PrintBucketStep"/> exists; a future phase adds Genre-Expansion and Discovery-Query
/// steps here without restructuring Stage 2 itself.
/// </summary>
public interface IBucketProcessingStep
{
    Task ProcessAsync(AggregatedBucket bucket, BucketProcessingState state, CancellationToken cancellationToken);
}
