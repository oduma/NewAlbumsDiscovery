using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Domain.Tests.MusicAggregator;

public class LovedTrackPreferencesTests
{
    [Fact]
    public void Constructor_WithValidInput_SetsProperties()
    {
        var preferences = new LovedTrackPreferences("Romania", ["English"], ["Rock", "Pop"]);

        Assert.Equal("Romania", preferences.Country);
        Assert.Equal(["English"], preferences.Languages);
        Assert.Equal(["Rock", "Pop"], preferences.Genres);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidCountry_Throws(string? country)
    {
        Assert.Throws<ArgumentException>(() => new LovedTrackPreferences(country!, [], []));
    }

    [Fact]
    public void Constructor_WithNullLanguages_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new LovedTrackPreferences("Romania", null!, []));
    }

    [Fact]
    public void Constructor_WithNullGenres_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new LovedTrackPreferences("Romania", [], null!));
    }

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        var first = new LovedTrackPreferences("Romania", ["English", "French"], ["Rock"]);
        var second = new LovedTrackPreferences("Romania", ["English", "French"], ["Rock"]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentLanguages_ReturnsFalse()
    {
        var first = new LovedTrackPreferences("Romania", ["English"], ["Rock"]);
        var second = new LovedTrackPreferences("Romania", ["French"], ["Rock"]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        var preferences = new LovedTrackPreferences("Romania", [], []);

        Assert.False(preferences.Equals(null));
        Assert.False(preferences.Equals((object?)null));
    }

    [Fact]
    public void Equals_WithSameReference_ReturnsTrue()
    {
        var preferences = new LovedTrackPreferences("Romania", [], []);

        Assert.True(preferences.Equals(preferences));
    }

    [Fact]
    public void Equals_WithNonMatchingType_ReturnsFalse()
    {
        var preferences = new LovedTrackPreferences("Romania", [], []);

        Assert.False(preferences.Equals(new object()));
    }
}
