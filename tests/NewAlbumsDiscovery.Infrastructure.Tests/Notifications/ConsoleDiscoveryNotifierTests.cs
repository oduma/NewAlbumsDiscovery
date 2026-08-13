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
    public async Task NotifyPipelineCompletedAsync_WritesProcessedCount()
    {
        var notifier = new ConsoleDiscoveryNotifier();

        var output = await CaptureConsoleOutputAsync(
            () => notifier.NotifyPipelineCompletedAsync(4, CancellationToken.None));

        Assert.Contains("4", output);
    }
}
