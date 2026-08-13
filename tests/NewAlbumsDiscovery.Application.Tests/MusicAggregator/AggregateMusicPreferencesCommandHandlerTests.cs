using Microsoft.Extensions.Options;
using Moq;
using NewAlbumsDiscovery.Application.MusicAggregator;
using NewAlbumsDiscovery.Domain.MusicAggregator;
using NewAlbumsDiscovery.Domain.MusicAggregator.Filtering;

namespace NewAlbumsDiscovery.Application.Tests.MusicAggregator;

public class AggregateMusicPreferencesCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 12, 3, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => FixedNow;
    }

    private static Mock<ICountryMasterDataProvider> NoOpMasterDataProvider()
    {
        var provider = new Mock<ICountryMasterDataProvider>();
        provider
            .Setup(p => p.GetCountrySynonymsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
        provider
            .Setup(p => p.GetCountryMasterDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CountryMasterData(new Dictionary<string, CountryMasterDataEntry>()));
        return provider;
    }

    private static AggregateMusicPreferencesCommandHandler CreateHandler(
        Mock<ILovedTrackRepository> lovedTrackRepository,
        Mock<IAggregatedBucketRepository> aggregatedBucketRepository,
        AggregatorSettings? settings = null,
        Mock<ICountryMasterDataProvider>? countryMasterDataProvider = null,
        IEnumerable<IBucketFilterRule>? filterRules = null)
        => new(
            lovedTrackRepository.Object,
            aggregatedBucketRepository.Object,
            new BucketAggregatorEngine(),
            Options.Create(settings ?? new AggregatorSettings()),
            new FixedTimeProvider(),
            (countryMasterDataProvider ?? NoOpMasterDataProvider()).Object,
            new CountryNormalizer(),
            filterRules ?? []);

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

    [Fact]
    public async Task Handle_NormalizesTrackCountriesBeforeAggregating()
    {
        var lovedTrackRepository = new Mock<ILovedTrackRepository>();
        var tracks = Enumerable.Range(0, 2)
            .Select(_ => new LovedTrackPreferences("United States", ["English"], ["Rock"]))
            .ToList();
        lovedTrackRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tracks);

        var masterDataProvider = NoOpMasterDataProvider();
        masterDataProvider
            .Setup(p => p.GetCountrySynonymsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["United States"] = "USA" });

        var aggregatedBucketRepository = CapturingAggregatedBucketRepository(out var getPersisted);
        var handler = CreateHandler(lovedTrackRepository, aggregatedBucketRepository,
            new AggregatorSettings { CountryRegionThreshold = 10, CountryRegionLanguageThreshold = 10, MinimumBucketThreshold = 2 },
            countryMasterDataProvider: masterDataProvider);

        await handler.Handle(new AggregateMusicPreferencesCommand(), CancellationToken.None);

        var bucket = Assert.Single(getPersisted()!);
        Assert.Equal("USA", bucket.Country);
    }

    [Fact]
    public async Task Handle_FoldsBucketsThroughFilterRulesInRegisteredOrder()
    {
        var lovedTrackRepository = new Mock<ILovedTrackRepository>();
        var tracks = Enumerable.Range(0, 2)
            .Select(_ => new LovedTrackPreferences("Malta", ["Maltese"], ["Folk"]))
            .ToList();
        lovedTrackRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tracks);

        var callOrder = new List<string>();
        var firstRule = new Mock<IBucketFilterRule>();
        firstRule
            .Setup(r => r.Apply(It.IsAny<IReadOnlyList<AggregatedBucket>>(), It.IsAny<CountryMasterData>()))
            .Returns((IReadOnlyList<AggregatedBucket> buckets, CountryMasterData _) =>
            {
                callOrder.Add("first");
                return buckets;
            });
        var secondRule = new Mock<IBucketFilterRule>();
        secondRule
            .Setup(r => r.Apply(It.IsAny<IReadOnlyList<AggregatedBucket>>(), It.IsAny<CountryMasterData>()))
            .Returns((IReadOnlyList<AggregatedBucket> buckets, CountryMasterData _) =>
            {
                callOrder.Add("second");
                return Array.Empty<AggregatedBucket>();
            });

        var aggregatedBucketRepository = CapturingAggregatedBucketRepository(out var getPersisted);
        var handler = CreateHandler(lovedTrackRepository, aggregatedBucketRepository,
            new AggregatorSettings { CountryRegionThreshold = 10, CountryRegionLanguageThreshold = 10, MinimumBucketThreshold = 2 },
            filterRules: [firstRule.Object, secondRule.Object]);

        await handler.Handle(new AggregateMusicPreferencesCommand(), CancellationToken.None);

        Assert.Equal(["first", "second"], callOrder);
        Assert.Empty(getPersisted()!);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToCountryMasterDataProvider()
    {
        var lovedTrackRepository = new Mock<ILovedTrackRepository>();
        lovedTrackRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LovedTrackPreferences>());
        var aggregatedBucketRepository = new Mock<IAggregatedBucketRepository>();
        aggregatedBucketRepository
            .Setup(r => r.ReplaceAllAsync(It.IsAny<IReadOnlyList<AggregatedBucket>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var masterDataProvider = NoOpMasterDataProvider();
        var handler = CreateHandler(lovedTrackRepository, aggregatedBucketRepository, countryMasterDataProvider: masterDataProvider);

        using var cts = new CancellationTokenSource();

        await handler.Handle(new AggregateMusicPreferencesCommand(), cts.Token);

        masterDataProvider.Verify(p => p.GetCountrySynonymsAsync(cts.Token), Times.Once);
        masterDataProvider.Verify(p => p.GetCountryMasterDataAsync(cts.Token), Times.Once);
    }
}
