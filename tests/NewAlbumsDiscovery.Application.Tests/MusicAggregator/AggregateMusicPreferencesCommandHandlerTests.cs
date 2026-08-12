using Microsoft.Extensions.Options;
using Moq;
using NewAlbumsDiscovery.Application.MusicAggregator;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.Tests.MusicAggregator;

public class AggregateMusicPreferencesCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 12, 3, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => FixedNow;
    }

    private static AggregateMusicPreferencesCommandHandler CreateHandler(
        Mock<ILovedTrackRepository> lovedTrackRepository,
        Mock<IAggregatedBucketRepository> aggregatedBucketRepository,
        AggregatorSettings? settings = null)
        => new(
            lovedTrackRepository.Object,
            aggregatedBucketRepository.Object,
            new BucketAggregatorEngine(),
            Options.Create(settings ?? new AggregatorSettings()),
            new FixedTimeProvider());

    private static Mock<IAggregatedBucketRepository> CapturingAggregatedBucketRepository(out Func<IReadOnlyList<AggregatedBucket>?> getPersisted)
    {
        IReadOnlyList<AggregatedBucket>? persisted = null;
        var repository = new Mock<IAggregatedBucketRepository>();
        repository
            .Setup(r => r.ReplaceAllAsync(It.IsAny<IReadOnlyList<AggregatedBucket>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<AggregatedBucket>, CancellationToken>((buckets, _) => persisted = buckets)
            .Returns(Task.CompletedTask);

        getPersisted = () => persisted;
        return repository;
    }

    [Fact]
    public async Task Handle_ReadsAllLovedTracksAndPersistsResultingBuckets()
    {
        var lovedTrackRepository = new Mock<ILovedTrackRepository>();
        var tracks = Enumerable.Range(0, 2)
            .Select(_ => new LovedTrackPreferences("Malta", ["Maltese"], ["Folk"]))
            .ToList();
        lovedTrackRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tracks);

        var aggregatedBucketRepository = CapturingAggregatedBucketRepository(out var getPersisted);
        var handler = CreateHandler(lovedTrackRepository, aggregatedBucketRepository,
            new AggregatorSettings { CountryRegionThreshold = 10, CountryRegionLanguageThreshold = 10, MinimumBucketThreshold = 2 });

        await handler.Handle(new AggregateMusicPreferencesCommand(), CancellationToken.None);

        var bucket = Assert.Single(getPersisted()!);
        Assert.Equal("Malta", bucket.Country);
        Assert.Equal(2, bucket.TrackCount);
    }

    [Fact]
    public async Task Handle_MapsAggregatorSettingsIntoThresholds()
    {
        var lovedTrackRepository = new Mock<ILovedTrackRepository>();
        var tracks = Enumerable.Range(0, 3)
            .Select(_ => new LovedTrackPreferences("Estonia", ["Estonian"], ["Pop"]))
            .ToList();
        lovedTrackRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tracks);

        var aggregatedBucketRepository = CapturingAggregatedBucketRepository(out var getPersisted);

        // CountryRegionThreshold = 3 means these 3 tracks no longer form a Country bucket, they cascade.
        var handler = CreateHandler(lovedTrackRepository, aggregatedBucketRepository,
            new AggregatorSettings { CountryRegionThreshold = 3, CountryRegionLanguageThreshold = 100, MinimumBucketThreshold = 1 });

        await handler.Handle(new AggregateMusicPreferencesCommand(), CancellationToken.None);

        var bucket = Assert.Single(getPersisted()!);
        Assert.Equal(BucketType.CountryLanguage, bucket.BucketType);
    }

    [Fact]
    public async Task Handle_UsesTimeProviderForCreatedAtUtc()
    {
        var lovedTrackRepository = new Mock<ILovedTrackRepository>();
        var tracks = Enumerable.Range(0, 2)
            .Select(_ => new LovedTrackPreferences("Malta", ["Maltese"], ["Folk"]))
            .ToList();
        lovedTrackRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tracks);

        var aggregatedBucketRepository = CapturingAggregatedBucketRepository(out var getPersisted);
        var handler = CreateHandler(lovedTrackRepository, aggregatedBucketRepository);

        await handler.Handle(new AggregateMusicPreferencesCommand(), CancellationToken.None);

        Assert.All(getPersisted()!, b => Assert.Equal(FixedNow.UtcDateTime, b.CreatedAtUtc));
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsOperationCanceled_PropagatesAndDoesNotPersist()
    {
        var lovedTrackRepository = new Mock<ILovedTrackRepository>();
        lovedTrackRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var aggregatedBucketRepository = new Mock<IAggregatedBucketRepository>();
        var handler = CreateHandler(lovedTrackRepository, aggregatedBucketRepository);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.Handle(new AggregateMusicPreferencesCommand(), cts.Token));

        aggregatedBucketRepository.Verify(
            r => r.ReplaceAllAsync(It.IsAny<IReadOnlyList<AggregatedBucket>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNoLovedTracks_StillReplacesWithEmptyList()
    {
        var lovedTrackRepository = new Mock<ILovedTrackRepository>();
        lovedTrackRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LovedTrackPreferences>());

        var aggregatedBucketRepository = new Mock<IAggregatedBucketRepository>();
        var handler = CreateHandler(lovedTrackRepository, aggregatedBucketRepository);

        await handler.Handle(new AggregateMusicPreferencesCommand(), CancellationToken.None);

        aggregatedBucketRepository.Verify(
            r => r.ReplaceAllAsync(It.Is<IReadOnlyList<AggregatedBucket>>(b => b.Count == 0), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenThroughToBothRepositories()
    {
        var lovedTrackRepository = new Mock<ILovedTrackRepository>();
        lovedTrackRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LovedTrackPreferences>());
        var aggregatedBucketRepository = new Mock<IAggregatedBucketRepository>();
        aggregatedBucketRepository
            .Setup(r => r.ReplaceAllAsync(It.IsAny<IReadOnlyList<AggregatedBucket>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = CreateHandler(lovedTrackRepository, aggregatedBucketRepository);

        using var cts = new CancellationTokenSource();

        await handler.Handle(new AggregateMusicPreferencesCommand(), cts.Token);

        lovedTrackRepository.Verify(r => r.GetAllAsync(cts.Token), Times.Once);
        aggregatedBucketRepository.Verify(
            r => r.ReplaceAllAsync(It.IsAny<IReadOnlyList<AggregatedBucket>>(), cts.Token), Times.Once);
    }
}
