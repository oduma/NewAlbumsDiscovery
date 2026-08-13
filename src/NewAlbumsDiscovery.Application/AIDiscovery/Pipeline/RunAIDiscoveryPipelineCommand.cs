using MediatR;
using NewAlbumsDiscovery.Application.MusicAggregator;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Runs one AIDiscovery pipeline pass over the current AggregatedBuckets: sorts them descending
/// by TrackCount, then folds that context through the ordered IAIDiscoveryStage pipeline
/// (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 5). No parameters — reads whatever
/// buckets currently exist at invocation time.
/// </summary>
public sealed record RunAIDiscoveryPipelineCommand : IRequest;

public sealed class RunAIDiscoveryPipelineCommandHandler : IRequestHandler<RunAIDiscoveryPipelineCommand>
{
    private readonly IAggregatedBucketRepository _bucketRepository;
    private readonly IEnumerable<IAIDiscoveryStage> _stages;

    public RunAIDiscoveryPipelineCommandHandler(
        IAggregatedBucketRepository bucketRepository,
        IEnumerable<IAIDiscoveryStage> stages)
    {
        _bucketRepository = bucketRepository;
        _stages = stages;
    }

    public async Task Handle(RunAIDiscoveryPipelineCommand request, CancellationToken cancellationToken)
    {
        var buckets = await _bucketRepository.GetAllAsync(cancellationToken);
        var sortedBuckets = buckets.OrderByDescending(b => b.TrackCount).ToList();

        var context = new AIDiscoveryPipelineContext(sortedBuckets);

        foreach (var stage in _stages)
        {
            context = await stage.ExecuteAsync(context, cancellationToken);
        }
    }
}
