namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Stage 1 (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 5 §3.2): announces the total
/// bucket count about to be processed, before any per-bucket work starts.
/// </summary>
public sealed class StartNotificationStage : IAIDiscoveryStage
{
    private readonly IDiscoveryNotifier _notifier;

    public StartNotificationStage(IDiscoveryNotifier notifier)
    {
        _notifier = notifier;
    }

    public async Task<AIDiscoveryPipelineContext> ExecuteAsync(AIDiscoveryPipelineContext context, CancellationToken cancellationToken)
    {
        await _notifier.NotifyPipelineStartingAsync(context.SortedBuckets.Count, cancellationToken);
        return context;
    }
}
