using Moq;
using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.Tests.AIDiscovery.Pipeline;

public class StartNotificationStageTests
{
    private static AggregatedBucket Bucket(int trackCount)
        => AggregatedBucket.Create("Bucket", BucketType.Country, "Netherlands", null, null, trackCount, DateTime.UtcNow);

    [Fact]
    public async Task ExecuteAsync_NotifiesWithBucketCount()
    {
        var notifier = new Mock<IDiscoveryNotifier>();
        var stage = new StartNotificationStage(notifier.Object);
        var context = new AIDiscoveryPipelineContext([Bucket(5), Bucket(3)]);

        await stage.ExecuteAsync(context, CancellationToken.None);

        notifier.Verify(n => n.NotifyPipelineStartingAsync(2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroBuckets_NotifiesWithZero()
    {
        var notifier = new Mock<IDiscoveryNotifier>();
        var stage = new StartNotificationStage(notifier.Object);
        var context = new AIDiscoveryPipelineContext([]);

        await stage.ExecuteAsync(context, CancellationToken.None);

        notifier.Verify(n => n.NotifyPipelineStartingAsync(0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSameContext()
    {
        var notifier = new Mock<IDiscoveryNotifier>();
        var stage = new StartNotificationStage(notifier.Object);
        var context = new AIDiscoveryPipelineContext([Bucket(1)]);

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(context, result);
    }
}
