using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Domain.Tests.MusicAggregator;

public class AggregatedBucketTests
{
    private static readonly DateTime AsOfUtc = new(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_GeneratesNonEmptyId()
    {
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 5, AsOfUtc);

        Assert.NotEqual(Guid.Empty, bucket.Id);
    }

    [Fact]
    public void Create_SetsAsOfUtcAsCreatedAtUtc()
    {
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 5, AsOfUtc);

        Assert.Equal(AsOfUtc, bucket.CreatedAtUtc);
    }

    [Fact]
    public void Constructor_WithExplicitId_UsesGivenId()
    {
        var id = Guid.NewGuid();

        var bucket = new AggregatedBucket(id, "Romania", BucketType.Country, "Romania", null, null, 5, AsOfUtc);

        Assert.Equal(id, bucket.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidBucketName_Throws(string? bucketName)
    {
        Assert.Throws<ArgumentException>(() =>
            new AggregatedBucket(Guid.NewGuid(), bucketName!, BucketType.Country, "Romania", null, null, 5, AsOfUtc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidCountry_Throws(string? country)
    {
        Assert.Throws<ArgumentException>(() =>
            new AggregatedBucket(Guid.NewGuid(), "Romania", BucketType.Country, country!, null, null, 5, AsOfUtc));
    }

    [Fact]
    public void Constructor_WithNegativeTrackCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AggregatedBucket(Guid.NewGuid(), "Romania", BucketType.Country, "Romania", null, null, -1, AsOfUtc));
    }

    [Fact]
    public void Constructor_WithZeroTrackCount_DoesNotThrow()
    {
        var bucket = new AggregatedBucket(Guid.NewGuid(), "Romania", BucketType.Country, "Romania", null, null, 0, AsOfUtc);

        Assert.Equal(0, bucket.TrackCount);
    }

    [Fact]
    public void Equals_WithSameId_ReturnsTrueRegardlessOfOtherProperties()
    {
        var id = Guid.NewGuid();
        var first = new AggregatedBucket(id, "Romania", BucketType.Country, "Romania", null, null, 5, AsOfUtc);
        var second = new AggregatedBucket(id, "Different", BucketType.CountryLanguage, "Other", "English", null, 99, AsOfUtc.AddDays(1));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentId_ReturnsFalse()
    {
        var first = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 5, AsOfUtc);
        var second = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 5, AsOfUtc);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 5, AsOfUtc);

        Assert.False(bucket.Equals(null));
        Assert.False(bucket.Equals((object?)null));
    }

    [Fact]
    public void Equals_WithNonMatchingType_ReturnsFalse()
    {
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 5, AsOfUtc);

        Assert.False(bucket.Equals(new object()));
    }

    [Fact]
    public void IsInstrumental_WithExactMatch_ReturnsTrue()
    {
        var bucket = AggregatedBucket.Create("Romania/Instrumental", BucketType.CountryLanguage, "Romania", "Instrumental", null, 5, AsOfUtc);

        Assert.True(bucket.IsInstrumental("Instrumental"));
    }

    [Fact]
    public void IsInstrumental_WithDifferentCase_ReturnsFalse()
    {
        var bucket = AggregatedBucket.Create("Romania/instrumental", BucketType.CountryLanguage, "Romania", "instrumental", null, 5, AsOfUtc);

        Assert.False(bucket.IsInstrumental("Instrumental"));
    }

    [Fact]
    public void IsInstrumental_WithNullLanguage_ReturnsFalse()
    {
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 5, AsOfUtc);

        Assert.False(bucket.IsInstrumental("Instrumental"));
    }

    [Fact]
    public void IsInstrumental_WithOtherLanguage_ReturnsFalse()
    {
        var bucket = AggregatedBucket.Create("Romania/English", BucketType.CountryLanguage, "Romania", "English", null, 5, AsOfUtc);

        Assert.False(bucket.IsInstrumental("Instrumental"));
    }

    [Fact]
    public void IsInstrumental_WithNullArgument_Throws()
    {
        var bucket = AggregatedBucket.Create("Romania/Instrumental", BucketType.CountryLanguage, "Romania", "Instrumental", null, 5, AsOfUtc);

        Assert.Throws<ArgumentNullException>(() => bucket.IsInstrumental(null!));
    }

    [Fact]
    public void IsInstrumental_WithNonDefaultConfiguredValue_MatchesConfiguredStringInstead()
    {
        var bucket = AggregatedBucket.Create("Romania/NoVocals", BucketType.CountryLanguage, "Romania", "NoVocals", null, 5, AsOfUtc);

        Assert.True(bucket.IsInstrumental("NoVocals"));
        Assert.False(bucket.IsInstrumental("Instrumental"));
    }
}
