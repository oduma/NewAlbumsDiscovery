using Microsoft.Extensions.Options;
using Moq;
using NewAlbumsDiscovery.Application.AIDiscovery;
using NewAlbumsDiscovery.Application.MusicAggregator;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.Tests.AIDiscovery;

public class DiscoverAlbumsCommandHandlerTests
{
    private static readonly DateTime AsOfUtc = new(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 13, 3, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => FixedNow;
    }

    private static AggregatedBucket Bucket(string country, string? language = null, string? genre = null)
        => AggregatedBucket.Create(country, BucketType.Country, country, language, genre, 5, AsOfUtc);

    private static Mock<IAggregatedBucketRepository> BucketRepository(params AggregatedBucket[] buckets)
    {
        var repository = new Mock<IAggregatedBucketRepository>();
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(buckets);
        return repository;
    }

    private static Mock<IDiscoveredAlbumRepository> ExistingAlbumRepository(params DiscoveredAlbum[] existing)
    {
        var repository = new Mock<IDiscoveredAlbumRepository>();
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        return repository;
    }

    private static Mock<IDiscoveredAlbumRepository> CapturingDiscoveredAlbumRepository(
        DiscoveredAlbum[] existing, out Func<IReadOnlyList<DiscoveredAlbum>?> getPersisted)
    {
        IReadOnlyList<DiscoveredAlbum>? persisted = null;
        var repository = new Mock<IDiscoveredAlbumRepository>();
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        repository
            .Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyList<DiscoveredAlbum>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<DiscoveredAlbum>, CancellationToken>((albums, _) => persisted = albums)
            .Returns(Task.CompletedTask);

        getPersisted = () => persisted;
        return repository;
    }

    private static DiscoverAlbumsCommandHandler CreateHandler(
        Mock<IAggregatedBucketRepository> bucketRepository,
        Mock<IDiscoveredAlbumRepository> discoveredAlbumRepository,
        Mock<IGeminiDiscoveryClient> geminiClient,
        GeminiOptions? options = null)
        => new(
            bucketRepository.Object,
            discoveredAlbumRepository.Object,
            geminiClient.Object,
            Options.Create(options ?? new GeminiOptions()),
            new FixedTimeProvider());

    [Fact]
    public async Task Handle_WithNoBuckets_NeverCallsGeminiOrPersists()
    {
        var bucketRepository = BucketRepository();
        var discoveredAlbumRepository = CapturingDiscoveredAlbumRepository([], out var getPersisted);
        var geminiClient = new Mock<IGeminiDiscoveryClient>();
        var handler = CreateHandler(bucketRepository, discoveredAlbumRepository, geminiClient);

        await handler.Handle(new DiscoverAlbumsCommand(), CancellationToken.None);

        geminiClient.Verify(
            c => c.DiscoverAsync(It.IsAny<DiscoveryPromptRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(getPersisted());
    }

    [Fact]
    public async Task Handle_WithAllNewCandidates_PersistsAllWithBucketContext()
    {
        var bucketRepository = BucketRepository(Bucket("Romania", "Romanian", "Rock"));
        var discoveredAlbumRepository = CapturingDiscoveredAlbumRepository([], out var getPersisted);
        var geminiClient = new Mock<IGeminiDiscoveryClient>();
        geminiClient
            .Setup(c => c.DiscoverAsync(It.IsAny<DiscoveryPromptRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CandidateAlbum("Artist A", "Album A")]);
        var handler = CreateHandler(bucketRepository, discoveredAlbumRepository, geminiClient);

        await handler.Handle(new DiscoverAlbumsCommand(), CancellationToken.None);

        var album = Assert.Single(getPersisted()!);
        Assert.Equal("Artist A", album.Artist);
        Assert.Equal("Album A", album.Album);
        Assert.Equal("Romania", album.Country);
        Assert.Equal("Romanian", album.Language);
        Assert.Equal("Rock", album.Genre);
        Assert.Equal(FixedNow.UtcDateTime, album.DiscoveredAtUtc);
    }

    [Fact]
    public async Task Handle_WithCandidateAlreadyKnown_DoesNotPersistIt()
    {
        var bucketRepository = BucketRepository(Bucket("Romania"));
        var existing = DiscoveredAlbum.Create("Artist A", "Album A", "Romania", null, null, AsOfUtc);
        var discoveredAlbumRepository = CapturingDiscoveredAlbumRepository([existing], out var getPersisted);
        var geminiClient = new Mock<IGeminiDiscoveryClient>();
        geminiClient
            .Setup(c => c.DiscoverAsync(It.IsAny<DiscoveryPromptRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CandidateAlbum("Artist A", "Album A")]);
        var handler = CreateHandler(bucketRepository, discoveredAlbumRepository, geminiClient);

        await handler.Handle(new DiscoverAlbumsCommand(), CancellationToken.None);

        Assert.Null(getPersisted());
    }

    [Fact]
    public async Task Handle_WithSameAlbumFromTwoBuckets_PersistsOnlyOnce()
    {
        var bucketRepository = BucketRepository(Bucket("Romania"), Bucket("Malta"));
        var discoveredAlbumRepository = CapturingDiscoveredAlbumRepository([], out var getPersisted);
        var geminiClient = new Mock<IGeminiDiscoveryClient>();
        geminiClient
            .Setup(c => c.DiscoverAsync(It.IsAny<DiscoveryPromptRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CandidateAlbum("Artist A", "Album A")]);
        var handler = CreateHandler(bucketRepository, discoveredAlbumRepository, geminiClient);

        await handler.Handle(new DiscoverAlbumsCommand(), CancellationToken.None);

        Assert.Single(getPersisted()!);
    }

    [Fact]
    public async Task Handle_WithEmptyCandidateListForABucket_ProcessesOtherBucketsWithoutError()
    {
        var bucketRepository = BucketRepository(Bucket("Romania"), Bucket("Malta"));
        var discoveredAlbumRepository = CapturingDiscoveredAlbumRepository([], out var getPersisted);
        var geminiClient = new Mock<IGeminiDiscoveryClient>();
        geminiClient
            .SetupSequence(c => c.DiscoverAsync(It.IsAny<DiscoveryPromptRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .ReturnsAsync([new CandidateAlbum("Artist B", "Album B")]);
        var handler = CreateHandler(bucketRepository, discoveredAlbumRepository, geminiClient);

        await handler.Handle(new DiscoverAlbumsCommand(), CancellationToken.None);

        var album = Assert.Single(getPersisted()!);
        Assert.Equal("Artist B", album.Artist);
    }

    [Fact]
    public async Task Handle_PassesBucketMaxAlbumsAndTimeframeFromOptions()
    {
        var bucketRepository = BucketRepository(Bucket("Romania", "Romanian", "Rock"));
        var discoveredAlbumRepository = ExistingAlbumRepository();
        var geminiClient = new Mock<IGeminiDiscoveryClient>();
        geminiClient
            .Setup(c => c.DiscoverAsync(It.IsAny<DiscoveryPromptRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var options = new GeminiOptions { MaxAlbumsPerPrompt = 7, Timeframe = "last 7 days" };
        var handler = CreateHandler(bucketRepository, discoveredAlbumRepository, geminiClient, options);

        await handler.Handle(new DiscoverAlbumsCommand(), CancellationToken.None);

        geminiClient.Verify(c => c.DiscoverAsync(
            It.Is<DiscoveryPromptRequest>(r =>
                r.Country == "Romania" && r.Language == "Romanian" && r.Genre == "Rock" &&
                r.MaxAlbums == 7 && r.Timeframe == "last 7 days"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
