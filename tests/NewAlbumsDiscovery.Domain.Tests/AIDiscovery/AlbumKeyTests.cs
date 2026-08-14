using NewAlbumsDiscovery.Domain.AIDiscovery;

namespace NewAlbumsDiscovery.Domain.Tests.AIDiscovery;

public class AlbumKeyTests
{
    [Fact]
    public void From_TrimsAndLowercasesArtistAndAlbum()
    {
        var key = AlbumKey.From("  Daft Punk  ", "  Discovery  ");

        Assert.Equal("daft punk", key.NormalizedArtist);
        Assert.Equal("discovery", key.NormalizedAlbum);
    }

    [Fact]
    public void From_DifferentCasingAndWhitespace_ProducesEqualKeys()
    {
        var first = AlbumKey.From("Daft Punk", "Discovery");
        var second = AlbumKey.From("  DAFT PUNK", "discovery  ");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void From_DifferentArtist_ProducesUnequalKeys()
    {
        var first = AlbumKey.From("Daft Punk", "Discovery");
        var second = AlbumKey.From("Justice", "Discovery");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void From_DifferentAlbum_ProducesUnequalKeys()
    {
        var first = AlbumKey.From("Daft Punk", "Discovery");
        var second = AlbumKey.From("Daft Punk", "Homework");

        Assert.NotEqual(first, second);
    }
}
