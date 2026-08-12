using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Domain.Tests.MusicAggregator;

public class BucketAggregatorEngineTests
{
    private static readonly DateTime AsOfUtc = new(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly AggregationThresholds Thresholds = new(
        countryRegionThreshold: 10,
        countryRegionLanguageThreshold: 10,
        minimumBucketThreshold: 2);

    private readonly BucketAggregatorEngine _engine = new();

    private static LovedTrackPreferences Track(string country, IReadOnlyList<string> languages, IReadOnlyList<string> genres)
        => new(country, languages, genres);

    [Fact]
    public void Aggregate_WithEmptyDataset_ReturnsEmptyList()
    {
        var result = _engine.Aggregate([], Thresholds, AsOfUtc);

        Assert.Empty(result);
    }

    [Fact]
    public void Aggregate_WithNullTracks_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.Aggregate(null!, Thresholds, AsOfUtc));
    }

    [Fact]
    public void Aggregate_WithNullThresholds_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.Aggregate([], null!, AsOfUtc));
    }

    [Fact]
    public void Aggregate_CountryCountBelowThreshold_FormsCountryBucket()
    {
        var tracks = Enumerable.Range(0, 9)
            .Select(_ => Track("Romania", ["English"], ["Rock"]))
            .ToList();

        var result = _engine.Aggregate(tracks, Thresholds, AsOfUtc);

        var bucket = Assert.Single(result);
        Assert.Equal(BucketType.Country, bucket.BucketType);
        Assert.Equal("Romania", bucket.BucketName);
        Assert.Equal("Romania", bucket.Country);
        Assert.Null(bucket.Language);
        Assert.Null(bucket.Genre);
        Assert.Equal(9, bucket.TrackCount);
    }

    [Fact]
    public void Aggregate_CountryCountAtThreshold_CascadesToLanguageLevel()
    {
        var tracks = Enumerable.Range(0, 5).Select(_ => Track("Romania", ["English"], ["Rock"]))
            .Concat(Enumerable.Range(0, 5).Select(_ => Track("Romania", ["French"], ["Rock"])))
            .ToList();

        var result = _engine.Aggregate(tracks, Thresholds, AsOfUtc);

        Assert.DoesNotContain(result, b => b.BucketType == BucketType.Country);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, b => b.BucketType == BucketType.CountryLanguage && b.BucketName == "Romania/English" && b.TrackCount == 5);
        Assert.Contains(result, b => b.BucketType == BucketType.CountryLanguage && b.BucketName == "Romania/French" && b.TrackCount == 5);
    }

    [Fact]
    public void Aggregate_CountryLanguageCountBelowThreshold_FormsCountryLanguageBucket()
    {
        var tracks = Enumerable.Range(0, 9).Select(_ => Track("Netherlands", ["Dutch"], ["Pop"]))
            .Concat(Enumerable.Range(0, 2).Select(_ => Track("Netherlands", ["Swahili"], ["Pop"])))
            .ToList();

        var result = _engine.Aggregate(tracks, Thresholds, AsOfUtc);

        Assert.DoesNotContain(result, b => b.BucketType is BucketType.Country or BucketType.CountryLanguageGenre);
        Assert.Contains(result, b => b.BucketType == BucketType.CountryLanguage && b.BucketName == "Netherlands/Dutch" && b.TrackCount == 9);
        Assert.Contains(result, b => b.BucketType == BucketType.CountryLanguage && b.BucketName == "Netherlands/Swahili" && b.TrackCount == 2);
    }

    [Fact]
    public void Aggregate_CountryLanguageCountAtThreshold_CascadesToGenreLevel()
    {
        var tracks = Enumerable.Range(0, 10)
            .Select(_ => Track("UK", ["English"], ["IndieRock"]))
            .ToList();

        var result = _engine.Aggregate(tracks, Thresholds, AsOfUtc);

        var bucket = Assert.Single(result);
        Assert.Equal(BucketType.CountryLanguageGenre, bucket.BucketType);
        Assert.Equal("UK/English/IndieRock", bucket.BucketName);
        Assert.Equal("UK", bucket.Country);
        Assert.Equal("English", bucket.Language);
        Assert.Equal("IndieRock", bucket.Genre);
        Assert.Equal(10, bucket.TrackCount);
    }

    [Fact]
    public void Aggregate_GenreLevel_NeverCascadesFurtherRegardlessOfCount()
    {
        var tracks = Enumerable.Range(0, 50)
            .Select(_ => Track("Japan", ["Japanese"], ["CityPop"]))
            .ToList();

        var result = _engine.Aggregate(tracks, Thresholds, AsOfUtc);

        var bucket = Assert.Single(result);
        Assert.Equal(BucketType.CountryLanguageGenre, bucket.BucketType);
        Assert.Equal(50, bucket.TrackCount);
    }

    [Fact]
    public void Aggregate_TrackWithMultipleLanguages_CountedOnceForEachLanguage()
    {
        var thresholds = new AggregationThresholds(
            countryRegionThreshold: 1,
            countryRegionLanguageThreshold: 100,
            minimumBucketThreshold: 1);
        var tracks = new[] { Track("Wales", ["English", "Welsh"], ["Folk"]) };

        var result = _engine.Aggregate(tracks, thresholds, AsOfUtc);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, b => b.BucketName == "Wales/English" && b.TrackCount == 1);
        Assert.Contains(result, b => b.BucketName == "Wales/Welsh" && b.TrackCount == 1);
    }

    [Fact]
    public void Aggregate_TrackWithMultipleGenres_CountedOnceForEachGenre()
    {
        var thresholds = new AggregationThresholds(
            countryRegionThreshold: 1,
            countryRegionLanguageThreshold: 1,
            minimumBucketThreshold: 1);
        var tracks = new[] { Track("UK", ["English"], ["Rock", "Pop", "Indie"]) };

        var result = _engine.Aggregate(tracks, thresholds, AsOfUtc);

        Assert.Equal(3, result.Count);
        Assert.All(result, b => Assert.Equal(BucketType.CountryLanguageGenre, b.BucketType));
        Assert.Contains(result, b => b.BucketName == "UK/English/Rock" && b.TrackCount == 1);
        Assert.Contains(result, b => b.BucketName == "UK/English/Pop" && b.TrackCount == 1);
        Assert.Contains(result, b => b.BucketName == "UK/English/Indie" && b.TrackCount == 1);
    }

    [Fact]
    public void Aggregate_SameLanguageAcrossDifferentCascadingCountries_ProducesSeparateBuckets()
    {
        var thresholds = new AggregationThresholds(
            countryRegionThreshold: 1,
            countryRegionLanguageThreshold: 100,
            minimumBucketThreshold: 1);
        var tracks = new[]
        {
            Track("France", ["English"], ["Rock"]),
            Track("Germany", ["English"], ["Rock"]),
        };

        var result = _engine.Aggregate(tracks, thresholds, AsOfUtc);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, b => b.BucketName == "France/English" && b.Country == "France" && b.TrackCount == 1);
        Assert.Contains(result, b => b.BucketName == "Germany/English" && b.Country == "Germany" && b.TrackCount == 1);
    }

    [Fact]
    public void Aggregate_BucketBelowMinimumThreshold_IsDiscarded()
    {
        var thresholds = new AggregationThresholds(
            countryRegionThreshold: 100,
            countryRegionLanguageThreshold: 100,
            minimumBucketThreshold: 2);
        var tracks = new[] { Track("Iceland", ["Icelandic"], ["Folk"]) };

        var result = _engine.Aggregate(tracks, thresholds, AsOfUtc);

        Assert.Empty(result);
    }

    [Fact]
    public void Aggregate_BucketAtMinimumThreshold_IsRetained()
    {
        var thresholds = new AggregationThresholds(
            countryRegionThreshold: 100,
            countryRegionLanguageThreshold: 100,
            minimumBucketThreshold: 2);
        var tracks = Enumerable.Range(0, 2)
            .Select(_ => Track("Malta", ["Maltese"], ["Folk"]))
            .ToList();

        var result = _engine.Aggregate(tracks, thresholds, AsOfUtc);

        var bucket = Assert.Single(result);
        Assert.Equal("Malta", bucket.Country);
        Assert.Equal(2, bucket.TrackCount);
    }

    [Fact]
    public void Aggregate_AsOfUtc_FlowsThroughToEveryReturnedBucket()
    {
        var customAsOfUtc = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tracks = Enumerable.Range(0, 2)
            .Select(_ => Track("Spain", ["Spanish"], ["Pop"]))
            .ToList();

        var result = _engine.Aggregate(tracks, Thresholds, customAsOfUtc);

        Assert.NotEmpty(result);
        Assert.All(result, b => Assert.Equal(customAsOfUtc, b.CreatedAtUtc));
    }
}
