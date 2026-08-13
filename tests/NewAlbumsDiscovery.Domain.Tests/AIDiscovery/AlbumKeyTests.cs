using NewAlbumsDiscovery.Domain.AIDiscovery;

namespace NewAlbumsDiscovery.Domain.Tests.AIDiscovery;

public class AlbumKeyTests
{
    [Fact]
    public void From_WithIdenticalInput_ProducesEqualKeys()
    {
        var first = AlbumKey.From("Angelo Badalamenti", "Twin Peaks");
        var second = AlbumKey.From("Angelo Badalamenti", "Twin Peaks");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void From_WithDifferentCasing_ProducesEqualKeys()
    {
        var first = AlbumKey.From("Angelo Badalamenti", "Twin Peaks");
        var second = AlbumKey.From("ANGELO BADALAMENTI", "twin peaks");

        Assert.Equal(first, second);
    }

    [Fact]
    public void From_WithSurroundingWhitespace_ProducesEqualKeys()
    {
        var first = AlbumKey.From("Angelo Badalamenti", "Twin Peaks");
        var second = AlbumKey.From("  Angelo Badalamenti  ", "  Twin Peaks  ");

        Assert.Equal(first, second);
    }

    [Fact]
    public void From_WithDifferentArtist_ProducesUnequalKeys()
    {
        var first = AlbumKey.From("Angelo Badalamenti", "Twin Peaks");
        var second = AlbumKey.From("David Lynch", "Twin Peaks");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void From_WithDifferentAlbum_ProducesUnequalKeys()
    {
        var first = AlbumKey.From("Angelo Badalamenti", "Twin Peaks");
        var second = AlbumKey.From("Angelo Badalamenti", "Blue Velvet");

        Assert.NotEqual(first, second);
    }
}
