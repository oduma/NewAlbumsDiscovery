using System.Globalization;
using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

namespace NewAlbumsDiscovery.Infrastructure.Notifications;

/// <summary>
/// Console implementation of IDiscoveryNotifier (docs/requirements/FUNCTIONAL_REQUIREMENTS.md →
/// Phase 5, extended in Phase 10). A future phase may add a sibling TelegramDiscoveryNotifier
/// implementing the same port to actually deliver these notices.
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

    public Task NotifyBucketAbandonedAsync(string bucketName, int trackCount, string reason, CancellationToken cancellationToken)
    {
        Console.WriteLine($"AIDiscovery: bucket '{bucketName}' ({trackCount} track(s)) ABANDONED - {reason}");
        return Task.CompletedTask;
    }

    public Task NotifyPipelineCompletedAsync(int processedBucketCount, int abandonedBucketCount, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Pipeline Complete. Total Buckets Processed: {processedBucketCount} | Total Buckets Abandoned: {abandonedBucketCount}");
        return Task.CompletedTask;
    }

    public Task NotifyBucketDiscoverySucceededAsync(string bucketName, int albumCount, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Processed Successfully - {albumCount} albums found");
        return Task.CompletedTask;
    }

    public Task NotifyDiscoveryReportAsync(DiscoveryRunReport report, CancellationToken cancellationToken)
    {
        Console.WriteLine("=== AIDiscovery Run Report ===");
        Console.WriteLine($"Total Buckets: {report.TotalBuckets}");
        Console.WriteLine($"Buckets Skipped: {report.BucketsSkipped}");
        Console.WriteLine($"Empty Buckets: {report.EmptyBuckets}");
        Console.WriteLine($"Total Albums Discovered: {report.TotalAlbumsDiscovered}");
        Console.WriteLine($"Average Albums per Bucket: {FormatAverage(report.AverageAlbumsPerBucket)}");
        Console.WriteLine($"Average Albums per Level 1 Bucket (Country): {FormatAverage(report.AverageAlbumsPerLevel1)}");
        Console.WriteLine($"Average Albums per Level 2 Bucket (Country+Language): {FormatAverage(report.AverageAlbumsPerLevel2)}");
        Console.WriteLine($"Average Albums per Level 3 Bucket (Country+Language+Genre): {FormatAverage(report.AverageAlbumsPerLevel3)}");
        Console.WriteLine($"Highest Yield Bucket: {FormatYield(report.HighestYieldBucketNames, report.HighestYieldCount)}");
        Console.WriteLine($"Lowest Yield Bucket: {FormatYield(report.LowestYieldBucketNames, report.LowestYieldCount)}");
        return Task.CompletedTask;
    }

    private static string FormatAverage(double? average)
        => average is null ? "N/A" : average.Value.ToString("F2", CultureInfo.InvariantCulture);

    private static string FormatYield(IReadOnlyList<string> bucketNames, int count)
        => bucketNames.Count == 0 ? "N/A" : $"{string.Join(", ", bucketNames)} ({count} albums)";
}
