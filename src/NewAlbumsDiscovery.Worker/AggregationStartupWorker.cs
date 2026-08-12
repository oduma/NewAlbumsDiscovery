using MediatR;
using NewAlbumsDiscovery.Application.MusicAggregator;

namespace NewAlbumsDiscovery.Worker;

/// <summary>
/// Runs the Feature 1 aggregation pipeline once per process start, on a background thread
/// (docs/requirements/phase3-requirements.md §2), alongside the untouched HeartbeatWorker. Real
/// periodic/monthly scheduling is deferred to a later orchestration phase.
/// </summary>
public sealed class AggregationStartupWorker(IServiceScopeFactory scopeFactory, ILogger<AggregationStartupWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(async () =>
        {
            logger.LogInformation("Starting music preference aggregation run.");

            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            await sender.Send(new AggregateMusicPreferencesCommand(), stoppingToken);

            logger.LogInformation("Music preference aggregation run completed.");
        }, stoppingToken);
}
