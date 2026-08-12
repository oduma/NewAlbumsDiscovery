namespace NewAlbumsDiscovery.Worker;

/// <summary>
/// Placeholder hosted service proving the cross-platform host runs on both Windows Service
/// and Linux daemon hosting models. Replaced by the real orchestrator in a later phase.
/// </summary>
public sealed class HeartbeatWorker(ILogger<HeartbeatWorker> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = configuration.GetValue("Heartbeat:IntervalSeconds", 60);

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Heartbeat at: {Time}", DateTimeOffset.Now);
            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }
    }
}
