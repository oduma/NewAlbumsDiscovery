namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Port for AIDiscovery pipeline progress signaling (Stage 1 start notice, Stage 2 per-bucket
/// progress, Stage 3 completion report). Implemented today as a console notifier; a future phase
/// may add a Telegram implementation of this same interface. Distinct from the future Feature 3
/// Notification bounded context (docs/specs/technical-specs.md §C), which separately batches and
/// dispatches DiscoveredAlbums — see docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 5 →
/// Design Notes for the boundary between the two.
/// </summary>
public interface IDiscoveryNotifier
{
    Task NotifyPipelineStartingAsync(int bucketCount, CancellationToken cancellationToken);

    Task NotifyBucketProcessedAsync(string bucketName, int trackCount, CancellationToken cancellationToken);

    Task NotifyPipelineCompletedAsync(int processedBucketCount, CancellationToken cancellationToken);
}
