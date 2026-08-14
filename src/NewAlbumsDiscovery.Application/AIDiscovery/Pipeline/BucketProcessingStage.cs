using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Domain.AIDiscovery;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Stage 2 (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 5 §3.3, extended in Phase 9/10):
/// visits every bucket in the context's already-sorted order, running each through the ordered
/// list of IBucketProcessingStep implementations against a fresh per-bucket BucketProcessingState,
/// pacing InterBucketDelaySeconds between buckets — never after the last one. If a step abandons
/// the bucket (state.IsAbandoned), remaining steps for that bucket are skipped and the bucket is
/// tallied in AbandonedBucketCount. The set of existing AlbumKeys is loaded once for the whole run
/// and threaded through every step call so AlbumPersistenceStep can dedup across buckets without a
/// DB round-trip per bucket (Phase 10). Every bucket's outcome (name, type, abandoned?, albums
/// discovered) is recorded into the returned context's BucketOutcomes for Stage 3's report.
/// </summary>
public sealed class BucketProcessingStage : IAIDiscoveryStage
{
    private readonly IEnumerable<IBucketProcessingStep> _steps;
    private readonly IDiscoveredAlbumRepository _albumRepository;
    private readonly IOptions<AIDiscoveryOptions> _options;
    private readonly TimeProvider _timeProvider;

    public BucketProcessingStage(
        IEnumerable<IBucketProcessingStep> steps,
        IDiscoveredAlbumRepository albumRepository,
        IOptions<AIDiscoveryOptions> options,
        TimeProvider timeProvider)
    {
        _steps = steps;
        _albumRepository = albumRepository;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<AIDiscoveryPipelineContext> ExecuteAsync(AIDiscoveryPipelineContext context, CancellationToken cancellationToken)
    {
        var buckets = context.SortedBuckets;
        var existingAlbumKeys = await _albumRepository.GetExistingAlbumKeysAsync(cancellationToken);
        var outcomes = new List<BucketOutcome>();
        var processedCount = 0;
        var abandonedCount = 0;

        for (var i = 0; i < buckets.Count; i++)
        {
            var bucket = buckets[i];
            var state = new BucketProcessingState();

            foreach (var step in _steps)
            {
                await step.ProcessAsync(bucket, state, existingAlbumKeys, cancellationToken);

                if (state.IsAbandoned)
                {
                    break;
                }
            }

            processedCount++;
            if (state.IsAbandoned)
            {
                abandonedCount++;
            }

            outcomes.Add(new BucketOutcome(bucket.BucketName, bucket.BucketType, state.IsAbandoned, state.PersistedAlbumCount));

            if (i < buckets.Count - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.Value.InterBucketDelaySeconds), _timeProvider, cancellationToken);
            }
        }

        return context with { ProcessedBucketCount = processedCount, AbandonedBucketCount = abandonedCount, BucketOutcomes = outcomes };
    }
}
