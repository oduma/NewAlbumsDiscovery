using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Stage 2 step, runs immediately after DiscoveryQueryPromptStep (docs/requirements/
/// FUNCTIONAL_REQUIREMENTS.md → Phase 10): deduplicates that bucket's candidate albums against
/// existingAlbumKeys (mutating it, so later buckets in the same run see these as already-known),
/// persists the newly-unique ones with Status = Pending, and reports the persisted count.
/// </summary>
public sealed class AlbumPersistenceStep : IBucketProcessingStep
{
    private readonly IDiscoveredAlbumRepository _repository;
    private readonly IDiscoveryNotifier _notifier;
    private readonly TimeProvider _timeProvider;

    public AlbumPersistenceStep(IDiscoveredAlbumRepository repository, IDiscoveryNotifier notifier, TimeProvider timeProvider)
    {
        _repository = repository;
        _notifier = notifier;
        _timeProvider = timeProvider;
    }

    public async Task ProcessAsync(AggregatedBucket bucket, BucketProcessingState state, ISet<AlbumKey> existingAlbumKeys, CancellationToken cancellationToken)
    {
        var newAlbums = AlbumDeduplicator.SelectNew(state.DiscoveredCandidates, existingAlbumKeys);

        if (newAlbums.Count > 0)
        {
            var discoveredAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var entities = newAlbums
                .Select(candidate => DiscoveredAlbum.Create(bucket.Id, candidate.Artist, candidate.Album, discoveredAtUtc))
                .ToList();
            await _repository.AddRangeAsync(entities, cancellationToken);
        }

        state.PersistedAlbumCount = newAlbums.Count;
        await _notifier.NotifyBucketDiscoverySucceededAsync(bucket.BucketName, newAlbums.Count, cancellationToken);
    }
}
