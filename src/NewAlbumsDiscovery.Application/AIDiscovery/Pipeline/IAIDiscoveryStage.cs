namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// One isolated stage of the AIDiscovery pipeline (Pipeline pattern per
/// docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 5 §3.1's design-pattern mandate). Stages
/// run in strict, DI-registration-determined order, each receiving the previous stage's returned
/// context.
/// </summary>
public interface IAIDiscoveryStage
{
    Task<AIDiscoveryPipelineContext> ExecuteAsync(AIDiscoveryPipelineContext context, CancellationToken cancellationToken);
}
