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
    public async Task NotifyPromptRenderedAsync_WritesHeaderThenContent()
    {
        var notifier = new ConsoleDiscoveryNotifier();

        var output = await CaptureConsoleOutputAsync(
            () => notifier.NotifyPromptRenderedAsync("--- PROMPT 2: DISCOVERY QUERY ---", "rendered prompt body", CancellationToken.None));

        var headerIndex = output.IndexOf("--- PROMPT 2: DISCOVERY QUERY ---", StringComparison.Ordinal);
        var contentIndex = output.IndexOf("rendered prompt body", StringComparison.Ordinal);
        Assert.True(headerIndex >= 0);
        Assert.True(contentIndex > headerIndex);
    }
}
