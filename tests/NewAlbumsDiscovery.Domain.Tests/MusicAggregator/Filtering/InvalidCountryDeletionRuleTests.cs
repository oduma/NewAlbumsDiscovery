using NewAlbumsDiscovery.Domain.MusicAggregator;
using NewAlbumsDiscovery.Domain.MusicAggregator.Filtering;

namespace NewAlbumsDiscovery.Domain.Tests.MusicAggregator.Filtering;

public class InvalidCountryDeletionRuleTests
{
    private static readonly DateTime AsOfUtc = new(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);

    private static CountryMasterData MasterData() => new(new Dictionary<string, CountryMasterDataEntry>
    {
        ["France"] = new CountryMasterDataEntry("Europe"),
        ["USA"] = new CountryMasterDataEntry("North America"),
    });

    private static AggregatedBucket CountryBucket(string country)
        => AggregatedBucket.Create(country, BucketType.Country, country, null, null, 5, AsOfUtc);

    private readonly InvalidCountryDeletionRule _rule = new();

    [Fact]
    public void Apply_BucketWithKnownCountry_IsRetained()
    {
        var buckets = new[] { CountryBucket("France") };

        var result = _rule.Apply(buckets, MasterData());

        Assert.Single(result);
    }

    [Fact]
    public void Apply_BucketWithKnownContinent_IsRetained()
    {
        var buckets = new[] { CountryBucket("Europe") };

        var result = _rule.Apply(buckets, MasterData());

        Assert.Single(result);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Global/Various")]
    [InlineData("Narnia")]
    public void Apply_BucketWithUnrecognizedCountry_IsDropped(string country)
    {
        var buckets = new[] { CountryBucket(country) };

        var result = _rule.Apply(buckets, MasterData());

        Assert.Empty(result);
    }

    [Fact]
    public void Apply_DifferentCaseKnownCountry_IsRetained()
    {
        var buckets = new[] { CountryBucket("france") };

        var result = _rule.Apply(buckets, MasterData());

        Assert.Single(result);
    }

    [Fact]
    public void Apply_WithEmptyBucketList_ReturnsEmptyResult()
    {
        var result = _rule.Apply([], MasterData());

        Assert.Empty(result);
    }
}
