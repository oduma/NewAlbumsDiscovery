using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

namespace NewAlbumsDiscovery.Infrastructure.Notifications;

/// <summary>
/// Console implementation of IDiscoveryNotifier (docs/requirements/FUNCTIONAL_REQUIREMENTS.md →
/// Phase 5). A future phase may add a sibling TelegramDiscoveryNotifier implementing the same
/// port to actually deliver these notices.
/// </summary>
public sealed class ConsoleDiscoveryNotifier : IDiscoveryNotifier
{
    public Task NotifyPipelineStartingAsync(int bucketCount, CancellationToken cancellationToken)
    {
        Console.WriteLine($"AIDiscovery: starting processing of {bucketCount} bucket(s).");
        return Task.CompletedTask;
    }

    public Task NotifyBucketProcessedAsync(string bucketName, int trackCount, CancellationToken cancellationToken)
    {
        Console.WriteLine($"AIDiscovery: processed bucket '{bucketName}' ({trackCount} track(s)).");
        return Task.CompletedTask;
    }

    public Task NotifyPipelineCompletedAsync(int processedBucketCount, CancellationToken cancellationToken)
    {
        Console.WriteLine($"AIDiscovery: completed. {processedBucketCount} bucket(s) processed.");
        return Task.CompletedTask;
    }
}
