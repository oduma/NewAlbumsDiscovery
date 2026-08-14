using Moq;
using NewAlbumsDiscovery.Application.AIDiscovery;
using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;
using NewAlbumsDiscovery.Application.Tests.TestSupport;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.Tests.AIDiscovery.Pipeline;

public class AlbumPersistenceStepTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero);

    private static AlbumPersistenceStep CreateStep(
        Mock<IDiscoveredAlbumRepository> repository, Mock<IDiscoveryNotifier> notifier, TimeProvider? timeProvider = null)
        => new(repository.Object, notifier.Object, timeProvider ?? new RecordingTimeProvider(FixedUtcNow));

    private static AggregatedBucket Bucket()
        => AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, DateTime.UtcNow);

    [Fact]
    public async Task ProcessAsync_WithNoCandidates_PersistsNothingAndNotifiesZero()
    {
        var repository = new Mock<IDiscoveredAlbumRepository>();
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(repository, notifier);
        var bucket = Bucket();
        var state = new BucketProcessingState();
        var existingKeys = new HashSet<AlbumKey>();

        await step.ProcessAsync(bucket, state, existingKeys, CancellationToken.None);

        repository.Verify(r => r.AddRangeAsync(It.IsAny<IReadOnlyList<DiscoveredAlbum>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(0, state.PersistedAlbumCount);
        notifier.Verify(n => n.NotifyBucketDiscoverySucceededAsync(bucket.BucketName, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithAllNewCandidates_PersistsAllAndGrowsExistingKeys()
    {
        var repository = new Mock<IDiscoveredAlbumRepository>();
        IReadOnlyList<DiscoveredAlbum>? persisted = null;
        repository
            .Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyList<DiscoveredAlbum>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<DiscoveredAlbum>, CancellationToken>((albums, _) => persisted = albums)
            .Returns(Task.CompletedTask);
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(repository, notifier);
        var bucket = Bucket();
        var state = new BucketProcessingState
        {
            DiscoveredCandidates = [("Daft Punk", "Discovery"), ("Justice", "Cross")],
        };
        var existingKeys = new HashSet<AlbumKey>();

        await step.ProcessAsync(bucket, state, existingKeys, CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal(2, persisted!.Count);
        Assert.Equal(2, state.PersistedAlbumCount);
        Assert.Equal(2, existingKeys.Count);
        Assert.All(persisted, a => Assert.Equal(bucket.Id, a.ReferenceBucketId));
        Assert.All(persisted, a => Assert.Equal(FixedUtcNow.UtcDateTime, a.DiscoveredAtUtc));
        notifier.Verify(n => n.NotifyBucketDiscoverySucceededAsync(bucket.BucketName, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithCandidateAlreadyInExistingKeys_PersistsOnlyTheNewOnes()
    {
        var repository = new Mock<IDiscoveredAlbumRepository>();
        IReadOnlyList<DiscoveredAlbum>? persisted = null;
        repository
            .Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyList<DiscoveredAlbum>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<DiscoveredAlbum>, CancellationToken>((albums, _) => persisted = albums)
            .Returns(Task.CompletedTask);
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(repository, notifier);
        var bucket = Bucket();
        var state = new BucketProcessingState
        {
            DiscoveredCandidates = [("Daft Punk", "Discovery"), ("Justice", "Cross")],
        };
        var existingKeys = new HashSet<AlbumKey> { AlbumKey.From("Daft Punk", "Discovery") };

        await step.ProcessAsync(bucket, state, existingKeys, CancellationToken.None);

        var only = Assert.Single(persisted!);
        Assert.Equal("Justice", only.Artist);
        Assert.Equal("Cross", only.AlbumName);
        Assert.Equal(1, state.PersistedAlbumCount);
    }

    [Fact]
    public async Task ProcessAsync_WithInBatchDuplicateCandidates_PersistsOnlyOne()
    {
        var repository = new Mock<IDiscoveredAlbumRepository>();
        IReadOnlyList<DiscoveredAlbum>? persisted = null;
        repository
            .Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyList<DiscoveredAlbum>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<DiscoveredAlbum>, CancellationToken>((albums, _) => persisted = albums)
            .Returns(Task.CompletedTask);
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(repository, notifier);
        var bucket = Bucket();
        var state = new BucketProcessingState
        {
            DiscoveredCandidates = [("Daft Punk", "Discovery"), ("DAFT PUNK", "discovery")],
        };
        var existingKeys = new HashSet<AlbumKey>();

        await step.ProcessAsync(bucket, state, existingKeys, CancellationToken.None);

        Assert.Single(persisted!);
        Assert.Equal(1, state.PersistedAlbumCount);
    }
}
