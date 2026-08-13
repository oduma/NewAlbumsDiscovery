using NewAlbumsDiscovery.Domain.MusicAggregator;
using NewAlbumsDiscovery.Domain.MusicAggregator.Filtering;

namespace NewAlbumsDiscovery.Domain.Tests.MusicAggregator.Filtering;

public class ContinentFallbackEliminationRuleTests
{
    private static readonly DateTime AsOfUtc = new(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);

    private static CountryMasterData MasterData() => new(new Dictionary<string, CountryMasterDataEntry>
    {
        ["France"] = new CountryMasterDataEntry("Europe"),
        ["Germany"] = new CountryMasterDataEntry("Europe"),
        ["Australia"] = new CountryMasterDataEntry("Oceania"),
        ["USA"] = new CountryMasterDataEntry("North America"),
    });

    private static AggregatedBucket Bucket(string bucketName, BucketType type, string country, string? language = null, string? genre = null, int trackCount = 5)
        => AggregatedBucket.Create(bucketName, type, country, language, genre, trackCount, AsOfUtc);

    private readonly ContinentFallbackEliminationRule _rule = new();

    [Fact]
    public void Apply_ContinentBucket_DroppedWhenCountryTypeBucketExistsInThatContinent()
    {
        var buckets = new[]
        {
            Bucket("Europe", BucketType.Country, "Europe"),
            Bucket("France", BucketType.Country, "France"),
        };

        var result = _rule.Apply(buckets, MasterData());

        Assert.DoesNotContain(result, b => b.Country == "Europe");
        Assert.Contains(result, b => b.Country == "France");
    }

    [Fact]
    public void Apply_ContinentBucket_DroppedWhenOnlyEvidenceIsCountryLanguageBucket()
    {
        var buckets = new[]
        {
            Bucket("Europe", BucketType.Country, "Europe"),
            Bucket("France/French", BucketType.CountryLanguage, "France", language: "French"),
        };

        var result = _rule.Apply(buckets, MasterData());

        Assert.DoesNotContain(result, b => b.Country == "Europe");
    }

    [Fact]
    public void Apply_ContinentBucket_DroppedWhenOnlyEvidenceIsCountryLanguageGenreBucket()
    {
        var buckets = new[]
        {
            Bucket("Europe", BucketType.Country, "Europe"),
            Bucket("France/French/Chanson", BucketType.CountryLanguageGenre, "France", language: "French", genre: "Chanson"),
        };

        var result = _rule.Apply(buckets, MasterData());

        Assert.DoesNotContain(result, b => b.Country == "Europe");
    }

    [Fact]
    public void Apply_ContinentBucket_RetainedWhenNoOtherBucketInThatContinent()
    {
        var buckets = new[]
        {
            Bucket("Europe", BucketType.Country, "Europe"),
            Bucket("USA", BucketType.Country, "USA"),
        };

        var result = _rule.Apply(buckets, MasterData());

        Assert.Contains(result, b => b.Country == "Europe");
        Assert.Contains(result, b => b.Country == "USA");
    }

    [Fact]
    public void Apply_AustraliaBucket_NeverDroppedRegardlessOfOtherBuckets()
    {
        var buckets = new[]
        {
            Bucket("Australia", BucketType.Country, "Australia"),
            Bucket("Europe", BucketType.Country, "Europe"),
            Bucket("France", BucketType.Country, "France"),
        };

        var result = _rule.Apply(buckets, MasterData());

        Assert.Contains(result, b => b.Country == "Australia");
    }

    [Fact]
    public void Apply_TwoDifferentContinents_EvaluatedIndependently()
    {
        var buckets = new[]
        {
            Bucket("Europe", BucketType.Country, "Europe"),
            Bucket("France", BucketType.Country, "France"),
            Bucket("North America", BucketType.Country, "North America"),
        };

        var result = _rule.Apply(buckets, MasterData());

        Assert.DoesNotContain(result, b => b.Country == "Europe");
        Assert.Contains(result, b => b.Country == "France");
        Assert.Contains(result, b => b.Country == "North America");
    }

    [Fact]
    public void Apply_WithEmptyBucketList_ReturnsEmptyResult()
    {
        var result = _rule.Apply([], MasterData());

        Assert.Empty(result);
    }
}
