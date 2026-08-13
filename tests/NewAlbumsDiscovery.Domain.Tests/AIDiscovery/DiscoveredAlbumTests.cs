using NewAlbumsDiscovery.Domain.AIDiscovery;

namespace NewAlbumsDiscovery.Domain.Tests.AIDiscovery;

public class DiscoveredAlbumTests
{
    private static readonly DateTime DiscoveredAtUtc = new(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_GeneratesNonEmptyId()
    {
        var album = DiscoveredAlbum.Create("Artist", "Album", "Romania", "Romanian", "Rock", DiscoveredAtUtc);

        Assert.NotEqual(Guid.Empty, album.Id);
    }

    [Fact]
    public void Create_SetsAllProperties()
    {
        var album = DiscoveredAlbum.Create("Artist", "Album", "Romania", "Romanian", "Rock", DiscoveredAtUtc);

        Assert.Equal("Artist", album.Artist);
        Assert.Equal("Album", album.Album);
        Assert.Equal("Romania", album.Country);
        Assert.Equal("Romanian", album.Language);
        Assert.Equal("Rock", album.Genre);
        Assert.Equal(DiscoveredAtUtc, album.DiscoveredAtUtc);
    }

    [Fact]
    public void Create_WithNullLanguageAndGenre_Succeeds()
    {
        var album = DiscoveredAlbum.Create("Artist", "Album", "Romania", null, null, DiscoveredAtUtc);

        Assert.Null(album.Language);
        Assert.Null(album.Genre);
    }

    [Fact]
    public void Constructor_WithExplicitId_UsesGivenId()
    {
        var id = Guid.NewGuid();

        var album = new DiscoveredAlbum(id, "Artist", "Album", "Romania", null, null, DiscoveredAtUtc);

        Assert.Equal(id, album.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidArtist_Throws(string? artist)
    {
        Assert.Throws<ArgumentException>(() =>
            new DiscoveredAlbum(Guid.NewGuid(), artist!, "Album", "Romania", null, null, DiscoveredAtUtc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidAlbum_Throws(string? album)
    {
        Assert.Throws<ArgumentException>(() =>
            new DiscoveredAlbum(Guid.NewGuid(), "Artist", album!, "Romania", null, null, DiscoveredAtUtc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidCountry_Throws(string? country)
    {
        Assert.Throws<ArgumentException>(() =>
            new DiscoveredAlbum(Guid.NewGuid(), "Artist", "Album", country!, null, null, DiscoveredAtUtc));
    }

    [Fact]
    public void Equals_WithSameId_ReturnsTrueRegardlessOfOtherProperties()
    {
        var id = Guid.NewGuid();
        var first = new DiscoveredAlbum(id, "Artist", "Album", "Romania", null, null, DiscoveredAtUtc);
        var second = new DiscoveredAlbum(id, "Other", "Other", "Malta", "Maltese", "Folk", DiscoveredAtUtc.AddDays(1));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentId_ReturnsFalse()
    {
        var first = DiscoveredAlbum.Create("Artist", "Album", "Romania", null, null, DiscoveredAtUtc);
        var second = DiscoveredAlbum.Create("Artist", "Album", "Romania", null, null, DiscoveredAtUtc);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        var album = DiscoveredAlbum.Create("Artist", "Album", "Romania", null, null, DiscoveredAtUtc);

        Assert.False(album.Equals(null));
        Assert.False(album.Equals((object?)null));
    }

    [Fact]
    public void Equals_WithNonMatchingType_ReturnsFalse()
    {
        var album = DiscoveredAlbum.Create("Artist", "Album", "Romania", null, null, DiscoveredAtUtc);

        Assert.False(album.Equals(new object()));
    }
}
