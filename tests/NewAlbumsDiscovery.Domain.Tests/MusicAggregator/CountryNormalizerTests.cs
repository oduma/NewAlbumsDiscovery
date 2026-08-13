using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Domain.Tests.MusicAggregator;

public class CountryNormalizerTests
{
    private readonly CountryNormalizer _normalizer = new();

    [Fact]
    public void Normalize_TrackWithExactSynonymMatch_RewritesToCanonicalCountry()
    {
        var tracks = new[] { new LovedTrackPreferences("United States", ["English"], ["Rock"]) };
        var synonyms = new Dictionary<string, string> { ["United States"] = "USA" };

        var result = _normalizer.Normalize(tracks, synonyms);

        var track = Assert.Single(result);
        Assert.Equal("USA", track.Country);
        Assert.Equal(["English"], track.Languages);
        Assert.Equal(["Rock"], track.Genres);
    }

    [Fact]
    public void Normalize_TrackWithCaseInsensitiveSynonymMatch_RewritesToCanonicalCountry()
    {
        var tracks = new[] { new LovedTrackPreferences("great britain", ["English"], ["Pop"]) };
        var synonyms = new Dictionary<string, string> { ["Great Britain"] = "UK" };

        var result = _normalizer.Normalize(tracks, synonyms);

        Assert.Equal("UK", Assert.Single(result).Country);
    }

    [Fact]
    public void Normalize_TrackWithNoSynonymEntry_PassesThroughUnchanged()
    {
        var tracks = new[] { new LovedTrackPreferences("France", ["French"], ["Chanson"]) };
        var synonyms = new Dictionary<string, string> { ["United States"] = "USA" };

        var result = _normalizer.Normalize(tracks, synonyms);

        var track = Assert.Single(result);
        Assert.Equal("France", track.Country);
    }

    [Fact]
    public void Normalize_WithEmptySynonyms_AllTracksPassThroughUnchanged()
    {
        var tracks = new[]
        {
            new LovedTrackPreferences("France", ["French"], ["Chanson"]),
            new LovedTrackPreferences("Germany", ["German"], ["Pop"]),
        };

        var result = _normalizer.Normalize(tracks, new Dictionary<string, string>());

        Assert.Equal(tracks, result);
    }

    [Fact]
    public void Normalize_WithEmptyTracks_ReturnsEmptyResult()
    {
        var result = _normalizer.Normalize([], new Dictionary<string, string>());

        Assert.Empty(result);
    }

    [Fact]
    public void Normalize_WithNullTracks_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _normalizer.Normalize(null!, new Dictionary<string, string>()));
    }

    [Fact]
    public void Normalize_WithNullSynonyms_Throws()
    {
        var tracks = new[] { new LovedTrackPreferences("France", ["French"], ["Chanson"]) };

        Assert.Throws<ArgumentNullException>(() => _normalizer.Normalize(tracks, null!));
    }
}
