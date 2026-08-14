using NewAlbumsDiscovery.Domain.AIDiscovery;

namespace NewAlbumsDiscovery.Domain.Tests.AIDiscovery;

public class DiscoveredAlbumTests
{
    private static readonly DateTime DiscoveredAtUtc = new(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_GeneratesNonEmptyId()
    {
        var album = DiscoveredAlbum.Create(Guid.NewGuid(), "Daft Punk", "Discovery", DiscoveredAtUtc);

        Assert.NotEqual(Guid.Empty, album.Id);
    }

    [Fact]
    public void Create_SetsStatusToPending()
    {
        var album = DiscoveredAlbum.Create(Guid.NewGuid(), "Daft Punk", "Discovery", DiscoveredAtUtc);

        Assert.Equal(DiscoveredAlbumStatus.Pending, album.Status);
    }

    [Fact]
    public void Create_SetsGivenReferenceBucketIdArtistAlbumNameAndDiscoveredAtUtc()
    {
        var bucketId = Guid.NewGuid();

        var album = DiscoveredAlbum.Create(bucketId, "Daft Punk", "Discovery", DiscoveredAtUtc);

        Assert.Equal(bucketId, album.ReferenceBucketId);
        Assert.Equal("Daft Punk", album.Artist);
        Assert.Equal("Discovery", album.AlbumName);
        Assert.Equal(DiscoveredAtUtc, album.DiscoveredAtUtc);
    }

    [Fact]
    public void Constructor_WithExplicitId_UsesGivenId()
    {
        var id = Guid.NewGuid();

        var album = new DiscoveredAlbum(id, Guid.NewGuid(), "Daft Punk", "Discovery", DiscoveredAlbumStatus.Pending, DiscoveredAtUtc);

        Assert.Equal(id, album.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidArtist_Throws(string? artist)
    {
        Assert.Throws<ArgumentException>(() =>
            new DiscoveredAlbum(Guid.NewGuid(), Guid.NewGuid(), artist!, "Discovery", DiscoveredAlbumStatus.Pending, DiscoveredAtUtc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidAlbumName_Throws(string? albumName)
    {
        Assert.Throws<ArgumentException>(() =>
            new DiscoveredAlbum(Guid.NewGuid(), Guid.NewGuid(), "Daft Punk", albumName!, DiscoveredAlbumStatus.Pending, DiscoveredAtUtc));
    }

    [Fact]
    public void Equals_WithSameId_ReturnsTrueRegardlessOfOtherProperties()
    {
        var id = Guid.NewGuid();
        var first = new DiscoveredAlbum(id, Guid.NewGuid(), "Daft Punk", "Discovery", DiscoveredAlbumStatus.Pending, DiscoveredAtUtc);
        var second = new DiscoveredAlbum(id, Guid.NewGuid(), "Justice", "Cross", DiscoveredAlbumStatus.Pending, DiscoveredAtUtc.AddDays(1));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentId_ReturnsFalse()
    {
        var first = DiscoveredAlbum.Create(Guid.NewGuid(), "Daft Punk", "Discovery", DiscoveredAtUtc);
        var second = DiscoveredAlbum.Create(Guid.NewGuid(), "Daft Punk", "Discovery", DiscoveredAtUtc);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        var album = DiscoveredAlbum.Create(Guid.NewGuid(), "Daft Punk", "Discovery", DiscoveredAtUtc);

        Assert.False(album.Equals(null));
        Assert.False(album.Equals((object?)null));
    }

    [Fact]
    public void Equals_WithNonMatchingType_ReturnsFalse()
    {
        var album = DiscoveredAlbum.Create(Guid.NewGuid(), "Daft Punk", "Discovery", DiscoveredAtUtc);

        Assert.False(album.Equals(new object()));
    }
}
