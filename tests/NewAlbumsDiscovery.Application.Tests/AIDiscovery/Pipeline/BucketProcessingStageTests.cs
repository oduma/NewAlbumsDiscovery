using Microsoft.Extensions.Options;
using Moq;
using NewAlbumsDiscovery.Application.AIDiscovery;
using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;
using NewAlbumsDiscovery.Application.Tests.TestSupport;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.Tests.AIDiscovery.Pipeline;

public class BucketProcessingStageTests
{
    private static AggregatedBucket Bucket(string name, int trackCount)
        => AggregatedBucket.Create(name, BucketType.Country, "Netherlands", null, null, trackCount, DateTime.UtcNow);

    private static Mock<IDiscoveredAlbumRepository> EmptyAlbumRepository()
    {
        var repository = new Mock<IDiscoveredAlbumRepository>();
        repository
            .Setup(r => r.GetExistingAlbumKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<AlbumKey>());
        return repository;
    }

    private static BucketProcessingStage CreateStage(
        IEnumerable<IBucketProcessingStep> steps,
        RecordingTimeProvider timeProvider,
        Mock<IDiscoveredAlbumRepository>? albumRepository = null,
        int interBucketDelaySeconds = 10)
        => new(
            steps,
            (albumRepository ?? EmptyAlbumRepository()).Object,
            Options.Create(new AIDiscoveryOptions { InterBucketDelaySeconds = interBucketDelaySeconds }),
            timeProvider);

    [Fact]
    public async Task ExecuteAsync_ZeroBuckets_NoStepCallsNoDelayZeroProcessed()
    {
        var step = new Mock<IBucketProcessingStep>();
        var timeProvider = new RecordingTimeProvider();
        var stage = CreateStage([step.Object], timeProvider);
        var context = new AIDiscoveryPipelineContext([]);

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        step.Verify(
            s => s.ProcessAsync(It.IsAny<AggregatedBucket>(), It.IsAny<BucketProcessingState>(), It.IsAny<ISet<AlbumKey>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Empty(timeProvider.Delays);
        Assert.Equal(0, result.ProcessedBucketCount);
        Assert.Equal(0, result.AbandonedBucketCount);
        Assert.Empty(result.BucketOutcomes);
    }

    [Fact]
    public async Task ExecuteAsync_OneBucket_StepRunsOnceNoDelay()
    {
        var step = new Mock<IBucketProcessingStep>();
        var timeProvider = new RecordingTimeProvider();
        var stage = CreateStage([step.Object], timeProvider);
        var bucket = Bucket("Only", 5);
        var context = new AIDiscoveryPipelineContext([bucket]);

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        step.Verify(
            s => s.ProcessAsync(bucket, It.IsAny<BucketProcessingState>(), It.IsAny<ISet<AlbumKey>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Empty(timeProvider.Delays);
        Assert.Equal(1, result.ProcessedBucketCount);
        Assert.Equal(0, result.AbandonedBucketCount);
    }

    [Fact]
    public async Task ExecuteAsync_TwoBuckets_DelayScheduledOnceNotAfterLast()
    {
        var step = new Mock<IBucketProcessingStep>();
        var timeProvider = new RecordingTimeProvider();
        var stage = CreateStage([step.Object], timeProvider, interBucketDelaySeconds: 10);
        var context = new AIDiscoveryPipelineContext([Bucket("A", 5), Bucket("B", 3)]);

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.Single(timeProvider.Delays);
        Assert.Equal(TimeSpan.FromSeconds(10), timeProvider.Delays[0]);
        Assert.Equal(2, result.ProcessedBucketCount);
    }

    [Fact]
    public async Task ExecuteAsync_ThreeBuckets_DelayScheduledExactlyTwice()
    {
        var step = new Mock<IBucketProcessingStep>();
        var timeProvider = new RecordingTimeProvider();
        var stage = CreateStage([step.Object], timeProvider);
        var context = new AIDiscoveryPipelineContext([Bucket("A", 5), Bucket("B", 3), Bucket("C", 1)]);

        await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(2, timeProvider.Delays.Count);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleSteps_AllRunInRegistrationOrderPerBucketBeforeNextBucket()
    {
        var callOrder = new List<string>();
        var stepA = new Mock<IBucketProcessingStep>();
        stepA.Setup(s => s.ProcessAsync(It.IsAny<AggregatedBucket>(), It.IsAny<BucketProcessingState>(), It.IsAny<ISet<AlbumKey>>(), It.IsAny<CancellationToken>()))
            .Callback<AggregatedBucket, BucketProcessingState, ISet<AlbumKey>, CancellationToken>((b, _, _, _) => callOrder.Add($"A:{b.BucketName}"))
            .Returns(Task.CompletedTask);
        var stepB = new Mock<IBucketProcessingStep>();
        stepB.Setup(s => s.ProcessAsync(It.IsAny<AggregatedBucket>(), It.IsAny<BucketProcessingState>(), It.IsAny<ISet<AlbumKey>>(), It.IsAny<CancellationToken>()))
            .Callback<AggregatedBucket, BucketProcessingState, ISet<AlbumKey>, CancellationToken>((b, _, _, _) => callOrder.Add($"B:{b.BucketName}"))
            .Returns(Task.CompletedTask);
        var timeProvider = new RecordingTimeProvider();
        var stage = CreateStage([stepA.Object, stepB.Object], timeProvider);
        var context = new AIDiscoveryPipelineContext([Bucket("First", 5), Bucket("Second", 3)]);

        await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(["A:First", "B:First", "A:Second", "B:Second"], callOrder);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesSortedBucketsOnReturnedContext()
    {
        var step = new Mock<IBucketProcessingStep>();
        var timeProvider = new RecordingTimeProvider();
        var stage = CreateStage([step.Object], timeProvider);
        var buckets = new List<AggregatedBucket> { Bucket("A", 5), Bucket("B", 3) };
        var context = new AIDiscoveryPipelineContext(buckets);

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(buckets, result.SortedBuckets);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAStepAbandonsTheBucket_SkipsRemainingStepsForThatBucketOnly()
    {
        var callOrder = new List<string>();
        var stepA = new Mock<IBucketProcessingStep>();
        stepA.Setup(s => s.ProcessAsync(It.IsAny<AggregatedBucket>(), It.IsAny<BucketProcessingState>(), It.IsAny<ISet<AlbumKey>>(), It.IsAny<CancellationToken>()))
            .Callback<AggregatedBucket, BucketProcessingState, ISet<AlbumKey>, CancellationToken>((b, _, _, _) => callOrder.Add($"A:{b.BucketName}"))
            .Returns(Task.CompletedTask);
        var stepB = new Mock<IBucketProcessingStep>();
        stepB.Setup(s => s.ProcessAsync(It.IsAny<AggregatedBucket>(), It.IsAny<BucketProcessingState>(), It.IsAny<ISet<AlbumKey>>(), It.IsAny<CancellationToken>()))
            .Callback<AggregatedBucket, BucketProcessingState, ISet<AlbumKey>, CancellationToken>((b, state, _, _) =>
            {
                callOrder.Add($"B:{b.BucketName}");
                if (b.BucketName == "First")
                {
                    state.Abandon();
                }
            })
            .Returns(Task.CompletedTask);
        var stepC = new Mock<IBucketProcessingStep>();
        stepC.Setup(s => s.ProcessAsync(It.IsAny<AggregatedBucket>(), It.IsAny<BucketProcessingState>(), It.IsAny<ISet<AlbumKey>>(), It.IsAny<CancellationToken>()))
            .Callback<AggregatedBucket, BucketProcessingState, ISet<AlbumKey>, CancellationToken>((b, _, _, _) => callOrder.Add($"C:{b.BucketName}"))
            .Returns(Task.CompletedTask);
        var timeProvider = new RecordingTimeProvider();
        var stage = CreateStage([stepA.Object, stepB.Object, stepC.Object], timeProvider);
        var context = new AIDiscoveryPipelineContext([Bucket("First", 5), Bucket("Second", 3)]);

        await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(["A:First", "B:First", "A:Second", "B:Second", "C:Second"], callOrder);
    }

    [Fact]
    public async Task ExecuteAsync_WithAbandonedBuckets_ReturnsCorrectAbandonedBucketCount()
    {
        var step = new Mock<IBucketProcessingStep>();
        step.Setup(s => s.ProcessAsync(It.IsAny<AggregatedBucket>(), It.IsAny<BucketProcessingState>(), It.IsAny<ISet<AlbumKey>>(), It.IsAny<CancellationToken>()))
            .Callback<AggregatedBucket, BucketProcessingState, ISet<AlbumKey>, CancellationToken>((b, state, _, _) =>
            {
                if (b.BucketName != "Second")
                {
                    state.Abandon();
                }
            })
            .Returns(Task.CompletedTask);
        var timeProvider = new RecordingTimeProvider();
        var stage = CreateStage([step.Object], timeProvider);
        var context = new AIDiscoveryPipelineContext([Bucket("First", 5), Bucket("Second", 3), Bucket("Third", 1)]);

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(3, result.ProcessedBucketCount);
        Assert.Equal(2, result.AbandonedBucketCount);
    }

    [Fact]
    public async Task ExecuteAsync_LoadsExistingAlbumKeysOnceAndPassesSameInstanceToEveryStepAndBucket()
    {
        var seenSets = new List<ISet<AlbumKey>>();
        var step = new Mock<IBucketProcessingStep>();
        step.Setup(s => s.ProcessAsync(It.IsAny<AggregatedBucket>(), It.IsAny<BucketProcessingState>(), It.IsAny<ISet<AlbumKey>>(), It.IsAny<CancellationToken>()))
            .Callback<AggregatedBucket, BucketProcessingState, ISet<AlbumKey>, CancellationToken>((_, _, keys, _) => seenSets.Add(keys))
            .Returns(Task.CompletedTask);
        var repository = new Mock<IDiscoveredAlbumRepository>();
        var seededKeys = new HashSet<AlbumKey> { AlbumKey.From("Daft Punk", "Discovery") };
        repository.Setup(r => r.GetExistingAlbumKeysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(seededKeys);
        var timeProvider = new RecordingTimeProvider();
        var stage = CreateStage([step.Object], timeProvider, repository);
        var context = new AIDiscoveryPipelineContext([Bucket("First", 5), Bucket("Second", 3)]);

        await stage.ExecuteAsync(context, CancellationToken.None);

        repository.Verify(r => r.GetExistingAlbumKeysAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, seenSets.Count);
        Assert.Same(seenSets[0], seenSets[1]);
        Assert.Same(seededKeys, seenSets[0]);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsBucketOutcomeForEveryBucketVisited()
    {
        var step = new Mock<IBucketProcessingStep>();
        step.Setup(s => s.ProcessAsync(It.IsAny<AggregatedBucket>(), It.IsAny<BucketProcessingState>(), It.IsAny<ISet<AlbumKey>>(), It.IsAny<CancellationToken>()))
            .Callback<AggregatedBucket, BucketProcessingState, ISet<AlbumKey>, CancellationToken>((b, state, _, _) =>
            {
                if (b.BucketName == "Abandoned")
                {
                    state.Abandon();
                }
                else
                {
                    state.PersistedAlbumCount = 3;
                }
            })
            .Returns(Task.CompletedTask);
        var timeProvider = new RecordingTimeProvider();
        var stage = CreateStage([step.Object], timeProvider);
        var context = new AIDiscoveryPipelineContext([Bucket("Normal", 5), Bucket("Abandoned", 3)]);

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(2, result.BucketOutcomes.Count);
        Assert.Equal(new BucketOutcome("Normal", BucketType.Country, WasAbandoned: false, AlbumsDiscovered: 3), result.BucketOutcomes[0]);
        Assert.Equal(new BucketOutcome("Abandoned", BucketType.Country, WasAbandoned: true, AlbumsDiscovered: 0), result.BucketOutcomes[1]);
    }
}
