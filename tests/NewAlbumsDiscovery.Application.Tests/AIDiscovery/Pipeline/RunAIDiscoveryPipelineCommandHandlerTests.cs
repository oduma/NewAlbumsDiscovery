using Moq;
using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;
using NewAlbumsDiscovery.Application.MusicAggregator;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.Tests.AIDiscovery.Pipeline;

public class RunAIDiscoveryPipelineCommandHandlerTests
{
    private static AggregatedBucket Bucket(string name, int trackCount)
        => AggregatedBucket.Create(name, BucketType.Country, "Netherlands", null, null, trackCount, DateTime.UtcNow);

    [Fact]
    public async Task Handle_SortsBucketsDescendingByTrackCountBeforeFirstStage()
    {
        var unsorted = new List<AggregatedBucket> { Bucket("Low", 2), Bucket("High", 10), Bucket("Mid", 5) };
        var bucketRepository = new Mock<IAggregatedBucketRepository>();
        bucketRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(unsorted);

        AIDiscoveryPipelineContext? observedContext = null;
        var stage = new Mock<IAIDiscoveryStage>();
        stage.Setup(s => s.ExecuteAsync(It.IsAny<AIDiscoveryPipelineContext>(), It.IsAny<CancellationToken>()))
            .Callback<AIDiscoveryPipelineContext, CancellationToken>((ctx, _) => observedContext = ctx)
            .ReturnsAsync((AIDiscoveryPipelineContext ctx, CancellationToken _) => ctx);

        var handler = new RunAIDiscoveryPipelineCommandHandler(bucketRepository.Object, [stage.Object]);

        await handler.Handle(new RunAIDiscoveryPipelineCommand(), CancellationToken.None);

        Assert.NotNull(observedContext);
        Assert.Equal(["High", "Mid", "Low"], observedContext!.SortedBuckets.Select(b => b.BucketName));
    }

    [Fact]
    public async Task Handle_RunsStagesInOrderThreadingContextThrough()
    {
        var bucketRepository = new Mock<IAggregatedBucketRepository>();
        bucketRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var callOrder = new List<string>();

        var stage1 = new Mock<IAIDiscoveryStage>();
        stage1.Setup(s => s.ExecuteAsync(It.IsAny<AIDiscoveryPipelineContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("stage1"))
            .ReturnsAsync((AIDiscoveryPipelineContext ctx, CancellationToken _) => ctx with { ProcessedBucketCount = 1 });

        var stage2 = new Mock<IAIDiscoveryStage>();
        AIDiscoveryPipelineContext? contextReceivedByStage2 = null;
        stage2.Setup(s => s.ExecuteAsync(It.IsAny<AIDiscoveryPipelineContext>(), It.IsAny<CancellationToken>()))
            .Callback<AIDiscoveryPipelineContext, CancellationToken>((ctx, _) =>
            {
                callOrder.Add("stage2");
                contextReceivedByStage2 = ctx;
            })
            .ReturnsAsync((AIDiscoveryPipelineContext ctx, CancellationToken _) => ctx);

        var stage3 = new Mock<IAIDiscoveryStage>();
        stage3.Setup(s => s.ExecuteAsync(It.IsAny<AIDiscoveryPipelineContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("stage3"))
            .ReturnsAsync((AIDiscoveryPipelineContext ctx, CancellationToken _) => ctx);

        var handler = new RunAIDiscoveryPipelineCommandHandler(bucketRepository.Object, [stage1.Object, stage2.Object, stage3.Object]);

        await handler.Handle(new RunAIDiscoveryPipelineCommand(), CancellationToken.None);

        Assert.Equal(["stage1", "stage2", "stage3"], callOrder);
        Assert.Equal(1, contextReceivedByStage2!.ProcessedBucketCount);
    }

    [Fact]
    public async Task Handle_ZeroBuckets_AllStagesStillRunOnceWithEmptyBucketList()
    {
        var bucketRepository = new Mock<IAggregatedBucketRepository>();
        bucketRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var stage = new Mock<IAIDiscoveryStage>();
        stage.Setup(s => s.ExecuteAsync(It.IsAny<AIDiscoveryPipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIDiscoveryPipelineContext ctx, CancellationToken _) => ctx);

        var handler = new RunAIDiscoveryPipelineCommandHandler(bucketRepository.Object, [stage.Object]);

        await handler.Handle(new RunAIDiscoveryPipelineCommand(), CancellationToken.None);

        stage.Verify(s => s.ExecuteAsync(
            It.Is<AIDiscoveryPipelineContext>(c => c.SortedBuckets.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
