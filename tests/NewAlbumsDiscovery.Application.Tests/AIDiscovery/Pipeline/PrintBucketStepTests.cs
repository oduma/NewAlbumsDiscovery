using Moq;
using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.Tests.AIDiscovery.Pipeline;

public class PrintBucketStepTests
{
    [Fact]
    public async Task ProcessAsync_NotifiesWithBucketNameAndTrackCount()
    {
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = new PrintBucketStep(notifier.Object);
        var bucket = AggregatedBucket.Create("Netherlands/Dutch", BucketType.CountryLanguage, "Netherlands", "Dutch", null, 12, DateTime.UtcNow);

        await step.ProcessAsync(bucket, CancellationToken.None);

        notifier.Verify(n => n.NotifyBucketProcessedAsync("Netherlands/Dutch", 12, It.IsAny<CancellationToken>()), Times.Once);
    }
}
