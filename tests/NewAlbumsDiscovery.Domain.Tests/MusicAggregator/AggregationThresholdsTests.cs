using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Domain.Tests.MusicAggregator;

public class AggregationThresholdsTests
{
    [Fact]
    public void Constructor_WithValidInput_SetsProperties()
    {
        var thresholds = new AggregationThresholds(10, 10, 2);

        Assert.Equal(10, thresholds.CountryRegionThreshold);
        Assert.Equal(10, thresholds.CountryRegionLanguageThreshold);
        Assert.Equal(2, thresholds.MinimumBucketThreshold);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveCountryRegionThreshold_Throws(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AggregationThresholds(value, 10, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveCountryRegionLanguageThreshold_Throws(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AggregationThresholds(10, value, 2));
    }

    [Fact]
    public void Constructor_WithNegativeMinimumBucketThreshold_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AggregationThresholds(10, 10, -1));
    }

    [Fact]
    public void Constructor_WithZeroMinimumBucketThreshold_DoesNotThrow()
    {
        var thresholds = new AggregationThresholds(10, 10, 0);

        Assert.Equal(0, thresholds.MinimumBucketThreshold);
    }

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        var first = new AggregationThresholds(10, 10, 2);
        var second = new AggregationThresholds(10, 10, 2);

        Assert.Equal(first, second);
    }
}
