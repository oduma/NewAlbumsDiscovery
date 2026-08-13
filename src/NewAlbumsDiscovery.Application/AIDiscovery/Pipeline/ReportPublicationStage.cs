namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Stage 3 (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 5 §3.4): reports how many buckets
/// were processed, signaling completion of this run.
/// </summary>
public sealed class ReportPublicationStage : IAIDiscoveryStage
{
    private readonly IDiscoveryNotifier _notifier;

    public ReportPublicationStage(IDiscoveryNotifier notifier)
    {
        _notifier = notifier;
    }

    public async Task<AIDiscoveryPipelineContext> ExecuteAsync(AIDiscoveryPipelineContext context, CancellationToken cancellationToken)
    {
        await _notifier.NotifyPipelineCompletedAsync(context.ProcessedBucketCount, cancellationToken);
        return context;
    }
}
