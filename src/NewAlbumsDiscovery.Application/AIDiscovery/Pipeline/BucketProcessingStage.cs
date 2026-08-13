using Microsoft.Extensions.Options;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Stage 2 (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 5 §3.3): visits every bucket in
/// the context's already-sorted order, running each through the ordered list of
/// IBucketProcessingStep implementations, pacing InterBucketDelaySeconds between buckets — never
/// after the last one.
/// </summary>
public sealed class BucketProcessingStage : IAIDiscoveryStage
{
    private readonly IEnumerable<IBucketProcessingStep> _steps;
    private readonly IOptions<AIDiscoveryOptions> _options;
    private readonly TimeProvider _timeProvider;

    public BucketProcessingStage(
        IEnumerable<IBucketProcessingStep> steps,
        IOptions<AIDiscoveryOptions> options,
        TimeProvider timeProvider)
    {
        _steps = steps;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<AIDiscoveryPipelineContext> ExecuteAsync(AIDiscoveryPipelineContext context, CancellationToken cancellationToken)
    {
        var buckets = context.SortedBuckets;
        var processedCount = 0;

        for (var i = 0; i < buckets.Count; i++)
        {
            var bucket = buckets[i];

            foreach (var step in _steps)
            {
                await step.ProcessAsync(bucket, cancellationToken);
            }

            processedCount++;

            if (i < buckets.Count - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.Value.InterBucketDelaySeconds), _timeProvider, cancellationToken);
            }
        }

        return context with { ProcessedBucketCount = processedCount };
    }
}
