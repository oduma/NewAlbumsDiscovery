using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;
using NewAlbumsDiscovery.Infrastructure.Notifications;

namespace NewAlbumsDiscovery.Infrastructure.Tests.Notifications;

public class ConsoleDiscoveryNotifierTests
{
    private static async Task<string> CaptureConsoleOutputAsync(Func<Task> action)
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            await action();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return writer.ToString();
    }

    [Fact]
    public async Task NotifyPipelineStartingAsync_WritesBucketCount()
    {
        var notifier = new ConsoleDiscoveryNotifier();

        var output = await CaptureConsoleOutputAsync(
            () => notifier.NotifyPipelineStartingAsync(7, CancellationToken.None));

        Assert.Contains("7", output);
    }

    [Fact]
    public async Task NotifyBucketProcessedAsync_WritesBucketNameAndTrackCount()
    {
        var notifier = new ConsoleDiscoveryNotifier();

        var output = await CaptureConsoleOutputAsync(
            () => notifier.NotifyBucketProcessedAsync("Netherlands/Dutch", 12, CancellationToken.None));

        Assert.Contains("Netherlands/Dutch", output);
        Assert.Contains("12", output);
    }

    [Fact]
    public async Task NotifyPipelineCompletedAsync_WritesProcessedAndAbandonedCounts()
    {
        var notifier = new ConsoleDiscoveryNotifier();

        var output = await CaptureConsoleOutputAsync(
            () => notifier.NotifyPipelineCompletedAsync(4, 1, CancellationToken.None));

        Assert.Contains("Total Buckets Processed: 4", output);
        Assert.Contains("Total Buckets Abandoned: 1", output);
    }

    [Fact]
    public async Task NotifyBucketAbandonedAsync_WritesBucketNameTrackCountAndReason()
    {
        var notifier = new ConsoleDiscoveryNotifier();

        var output = await CaptureConsoleOutputAsync(
            () => notifier.NotifyBucketAbandonedAsync("USA/English/Indie Pop", 9, "exhausted retries", CancellationToken.None));

        Assert.Contains("USA/English/Indie Pop", output);
        Assert.Contains("9", output);
        Assert.Contains("exhausted retries", output);
    }

    [Fact]
    public async Task NotifyBucketDiscoverySucceededAsync_WritesAlbumCount()
    {
        var notifier = new ConsoleDiscoveryNotifier();

        var output = await CaptureConsoleOutputAsync(
            () => notifier.NotifyBucketDiscoverySucceededAsync("USA/English/Indie Pop", 5, CancellationToken.None));

        Assert.Contains("Processed Successfully - 5 albums found", output);
    }

    [Fact]
    public async Task NotifyDiscoveryReportAsync_WithFullMetrics_WritesAllTenMetrics()
    {
        var notifier = new ConsoleDiscoveryNotifier();
        var report = new DiscoveryRunReport(
            TotalBuckets: 10,
            BucketsSkipped: 2,
            EmptyBuckets: 1,
            TotalAlbumsDiscovered: 42,
            AverageAlbumsPerBucket: 6.0,
            AverageAlbumsPerLevel1: 4.5,
            AverageAlbumsPerLevel2: 7.25,
            AverageAlbumsPerLevel3: 8.0,
            HighestYieldBucketNames: ["USA/English/Indie Pop"],
            HighestYieldCount: 12,
            LowestYieldBucketNames: ["Malta"],
            LowestYieldCount: 1);

        var output = await CaptureConsoleOutputAsync(
            () => notifier.NotifyDiscoveryReportAsync(report, CancellationToken.None));

        Assert.Contains("Total Buckets: 10", output);
        Assert.Contains("Buckets Skipped: 2", output);
        Assert.Contains("Empty Buckets: 1", output);
        Assert.Contains("Total Albums Discovered: 42", output);
        Assert.Contains("Average Albums per Bucket: 6.00", output);
        Assert.Contains("Average Albums per Level 1 Bucket (Country): 4.50", output);
        Assert.Contains("Average Albums per Level 2 Bucket (Country+Language): 7.25", output);
        Assert.Contains("Average Albums per Level 3 Bucket (Country+Language+Genre): 8.00", output);
        Assert.Contains("Highest Yield Bucket: USA/English/Indie Pop (12 albums)", output);
        Assert.Contains("Lowest Yield Bucket: Malta (1 albums)", output);
    }

    [Fact]
    public async Task NotifyDiscoveryReportAsync_WithNoEligibleBuckets_WritesNAForAveragesAndYields()
    {
        var notifier = new ConsoleDiscoveryNotifier();
        var report = new DiscoveryRunReport(
            TotalBuckets: 2,
            BucketsSkipped: 2,
            EmptyBuckets: 0,
            TotalAlbumsDiscovered: 0,
            AverageAlbumsPerBucket: null,
            AverageAlbumsPerLevel1: null,
            AverageAlbumsPerLevel2: null,
            AverageAlbumsPerLevel3: null,
            HighestYieldBucketNames: [],
            HighestYieldCount: 0,
            LowestYieldBucketNames: [],
            LowestYieldCount: 0);

        var output = await CaptureConsoleOutputAsync(
            () => notifier.NotifyDiscoveryReportAsync(report, CancellationToken.None));

        Assert.Contains("Average Albums per Bucket: N/A", output);
        Assert.Contains("Highest Yield Bucket: N/A", output);
        Assert.Contains("Lowest Yield Bucket: N/A", output);
    }

    [Fact]
    public async Task NotifyDiscoveryReportAsync_WithTiedYieldBuckets_JoinsAllNamesWithCommas()
    {
        var notifier = new ConsoleDiscoveryNotifier();
        var report = new DiscoveryRunReport(
            TotalBuckets: 2,
            BucketsSkipped: 0,
            EmptyBuckets: 0,
            TotalAlbumsDiscovered: 10,
            AverageAlbumsPerBucket: 5.0,
            AverageAlbumsPerLevel1: 5.0,
            AverageAlbumsPerLevel2: null,
            AverageAlbumsPerLevel3: null,
            HighestYieldBucketNames: ["A", "B"],
            HighestYieldCount: 5,
            LowestYieldBucketNames: ["A", "B"],
            LowestYieldCount: 5);

        var output = await CaptureConsoleOutputAsync(
            () => notifier.NotifyDiscoveryReportAsync(report, CancellationToken.None));

        Assert.Contains("Highest Yield Bucket: A, B (5 albums)", output);
    }
}
