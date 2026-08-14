using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Stage 2's only step this phase (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 5 §3.3):
/// reports the bucket's name and track count. Future phases add Genre-Expansion/Discovery-Query
/// steps alongside this one.
/// </summary>
public sealed class PrintBucketStep : IBucketProcessingStep
{
    private readonly IDiscoveryNotifier _notifier;

    public PrintBucketStep(IDiscoveryNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task ProcessAsync(AggregatedBucket bucket, BucketProcessingState state, ISet<AlbumKey> existingAlbumKeys, CancellationToken cancellationToken)
        => _notifier.NotifyBucketProcessedAsync(bucket.BucketName, bucket.TrackCount, cancellationToken);
}
