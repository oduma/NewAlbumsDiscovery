using MediatR;
using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;
using NewAlbumsDiscovery.Application.MusicAggregator;

namespace NewAlbumsDiscovery.Application.CoreOperations;

/// <summary>
/// Cross-context sequencing command — the seed of the "central orchestrator"
/// docs/specs/technical-specs.md describes as eventually coordinating Features 1, 2, and 3
/// sequentially (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 5). Runs
/// AggregateMusicPreferencesCommand, waits TriggerDelaySeconds, then runs
/// RunAIDiscoveryPipelineCommand. A future phase is expected to append a third sequential send
/// here once the Notification (Feature 3) context exists. Only ever sequences other contexts'
/// MediatR commands via ISender — never reaches into their domain/repository types directly.
/// </summary>
public sealed record RunOrchestrationCommand : IRequest;

public sealed class RunOrchestrationCommandHandler : IRequestHandler<RunOrchestrationCommand>
{
    private readonly ISender _sender;
    private readonly IOptions<CoreOperationsOptions> _options;
    private readonly TimeProvider _timeProvider;

    public RunOrchestrationCommandHandler(
        ISender sender,
        IOptions<CoreOperationsOptions> options,
        TimeProvider timeProvider)
    {
        _sender = sender;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task Handle(RunOrchestrationCommand request, CancellationToken cancellationToken)
    {
        await _sender.Send(new AggregateMusicPreferencesCommand(), cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(_options.Value.TriggerDelaySeconds), _timeProvider, cancellationToken);
        await _sender.Send(new RunAIDiscoveryPipelineCommand(), cancellationToken);
    }
}
