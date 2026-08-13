using MediatR;
using NewAlbumsDiscovery.Application.CoreOperations;

namespace NewAlbumsDiscovery.Worker;

/// <summary>
/// Runs Features 1 (Aggregation) and 2 (AIDiscovery) sequentially, once per process start, on a
/// background thread, alongside the untouched HeartbeatWorker (docs/requirements/
/// FUNCTIONAL_REQUIREMENTS.md → Phase 5). Replaces the Phase 3 AggregationStartupWorker: all
/// sequencing/timing logic (including the 30s post-aggregation delay) lives in
/// RunOrchestrationCommandHandler, not here — this stays a thin trigger, matching the file it
/// replaces. A future phase is expected to have Feature 3 (Notification) join the sequence inside
/// that handler. Real periodic/monthly scheduling remains deferred.
/// </summary>
public sealed class OrchestrationStartupWorker(IServiceScopeFactory scopeFactory, ILogger<OrchestrationStartupWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(async () =>
        {
            logger.LogInformation("Starting orchestration run.");

            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            await sender.Send(new RunOrchestrationCommand(), stoppingToken);

            logger.LogInformation("Orchestration run completed.");
        }, stoppingToken);
}
