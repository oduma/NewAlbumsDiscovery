using Moq;
using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

namespace NewAlbumsDiscovery.Application.Tests.AIDiscovery.Pipeline;

public class ReportPublicationStageTests
{
    [Fact]
    public async Task ExecuteAsync_NotifiesWithProcessedBucketCount()
    {
        var notifier = new Mock<IDiscoveryNotifier>();
        var stage = new ReportPublicationStage(notifier.Object);
        var context = new AIDiscoveryPipelineContext([], ProcessedBucketCount: 4);

        await stage.ExecuteAsync(context, CancellationToken.None);

        notifier.Verify(n => n.NotifyPipelineCompletedAsync(4, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSameContext()
    {
        var notifier = new Mock<IDiscoveryNotifier>();
        var stage = new ReportPublicationStage(notifier.Object);
        var context = new AIDiscoveryPipelineContext([], ProcessedBucketCount: 2);

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(context, result);
    }
}
