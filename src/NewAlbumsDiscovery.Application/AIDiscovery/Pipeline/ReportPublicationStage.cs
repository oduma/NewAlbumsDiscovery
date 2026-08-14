namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Stage 3 (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 5 §3.4, extended in Phase 10):
/// computes the run's statistical report from the context's BucketOutcomes and publishes it,
/// then reports how many buckets were processed/abandoned, signaling completion of this run.
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
        var report = DiscoveryRunReportCalculator.Calculate(context.BucketOutcomes);
        await _notifier.NotifyDiscoveryReportAsync(report, cancellationToken);
        await _notifier.NotifyPipelineCompletedAsync(context.ProcessedBucketCount, context.AbandonedBucketCount, cancellationToken);
        return context;
    }
}
