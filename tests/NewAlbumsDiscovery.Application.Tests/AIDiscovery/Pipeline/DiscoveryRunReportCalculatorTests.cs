using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.Tests.AIDiscovery.Pipeline;

public class DiscoveryRunReportCalculatorTests
{
    [Fact]
    public void Calculate_WithEmptyOutcomes_ReturnsAllZerosAndNoEligibleBuckets()
    {
        var report = DiscoveryRunReportCalculator.Calculate([]);

        Assert.Equal(0, report.TotalBuckets);
        Assert.Equal(0, report.BucketsSkipped);
        Assert.Equal(0, report.EmptyBuckets);
        Assert.Equal(0, report.TotalAlbumsDiscovered);
        Assert.Null(report.AverageAlbumsPerBucket);
        Assert.Null(report.AverageAlbumsPerLevel1);
        Assert.Null(report.AverageAlbumsPerLevel2);
        Assert.Null(report.AverageAlbumsPerLevel3);
        Assert.Empty(report.HighestYieldBucketNames);
        Assert.Empty(report.LowestYieldBucketNames);
    }

    [Fact]
    public void Calculate_WithAllBucketsAbandoned_CountsSkippedAndHasNoEligibleAverages()
    {
        var outcomes = new List<BucketOutcome>
        {
            new("A", BucketType.Country, WasAbandoned: true, AlbumsDiscovered: 0),
            new("B", BucketType.Country, WasAbandoned: true, AlbumsDiscovered: 0),
        };

        var report = DiscoveryRunReportCalculator.Calculate(outcomes);

        Assert.Equal(2, report.TotalBuckets);
        Assert.Equal(2, report.BucketsSkipped);
        Assert.Equal(0, report.EmptyBuckets);
        Assert.Null(report.AverageAlbumsPerBucket);
        Assert.Empty(report.HighestYieldBucketNames);
        Assert.Empty(report.LowestYieldBucketNames);
    }

    [Fact]
    public void Calculate_WithAllBucketsEmptyButNotAbandoned_CountsEmptyAndHasNoEligibleAverages()
    {
        var outcomes = new List<BucketOutcome>
        {
            new("A", BucketType.Country, WasAbandoned: false, AlbumsDiscovered: 0),
            new("B", BucketType.Country, WasAbandoned: false, AlbumsDiscovered: 0),
        };

        var report = DiscoveryRunReportCalculator.Calculate(outcomes);

        Assert.Equal(2, report.TotalBuckets);
        Assert.Equal(0, report.BucketsSkipped);
        Assert.Equal(2, report.EmptyBuckets);
        Assert.Null(report.AverageAlbumsPerBucket);
        Assert.Empty(report.HighestYieldBucketNames);
    }

    [Fact]
    public void Calculate_WithMixedRunAcrossAllThreeLevels_ComputesEachMetricCorrectly()
    {
        var outcomes = new List<BucketOutcome>
        {
            new("CountryOnly", BucketType.Country, WasAbandoned: false, AlbumsDiscovered: 4),
            new("CountryLang", BucketType.CountryLanguage, WasAbandoned: false, AlbumsDiscovered: 8),
            new("CountryLangGenreHigh", BucketType.CountryLanguageGenre, WasAbandoned: false, AlbumsDiscovered: 12),
            new("CountryLangGenreLow", BucketType.CountryLanguageGenre, WasAbandoned: false, AlbumsDiscovered: 2),
            new("Empty", BucketType.CountryLanguageGenre, WasAbandoned: false, AlbumsDiscovered: 0),
            new("Skipped", BucketType.CountryLanguageGenre, WasAbandoned: true, AlbumsDiscovered: 0),
        };

        var report = DiscoveryRunReportCalculator.Calculate(outcomes);

        Assert.Equal(6, report.TotalBuckets);
        Assert.Equal(1, report.BucketsSkipped);
        Assert.Equal(1, report.EmptyBuckets);
        Assert.Equal(26, report.TotalAlbumsDiscovered);
        Assert.Equal((4 + 8 + 12 + 2) / 4.0, report.AverageAlbumsPerBucket);
        Assert.Equal(4.0, report.AverageAlbumsPerLevel1);
        Assert.Equal(8.0, report.AverageAlbumsPerLevel2);
        Assert.Equal((12 + 2) / 2.0, report.AverageAlbumsPerLevel3);
        Assert.Equal(["CountryLangGenreHigh"], report.HighestYieldBucketNames);
        Assert.Equal(12, report.HighestYieldCount);
        Assert.Equal(["CountryLangGenreLow"], report.LowestYieldBucketNames);
        Assert.Equal(2, report.LowestYieldCount);
    }

    [Fact]
    public void Calculate_WithOnlyOneBucketTypePresent_LeavesOtherLevelAveragesNull()
    {
        var outcomes = new List<BucketOutcome>
        {
            new("A", BucketType.Country, WasAbandoned: false, AlbumsDiscovered: 5),
        };

        var report = DiscoveryRunReportCalculator.Calculate(outcomes);

        Assert.Equal(5.0, report.AverageAlbumsPerLevel1);
        Assert.Null(report.AverageAlbumsPerLevel2);
        Assert.Null(report.AverageAlbumsPerLevel3);
    }

    [Fact]
    public void Calculate_WithThreeWayTieForHighestYield_ListsAllTiedNamesInSourceOrder()
    {
        var outcomes = new List<BucketOutcome>
        {
            new("A", BucketType.Country, WasAbandoned: false, AlbumsDiscovered: 5),
            new("B", BucketType.Country, WasAbandoned: false, AlbumsDiscovered: 5),
            new("C", BucketType.Country, WasAbandoned: false, AlbumsDiscovered: 5),
        };

        var report = DiscoveryRunReportCalculator.Calculate(outcomes);

        Assert.Equal(["A", "B", "C"], report.HighestYieldBucketNames);
        Assert.Equal(5, report.HighestYieldCount);
        Assert.Equal(["A", "B", "C"], report.LowestYieldBucketNames);
        Assert.Equal(5, report.LowestYieldCount);
    }

    [Fact]
    public void Calculate_WithTwoWayTieForLowestYield_ListsBothTiedNames()
    {
        var outcomes = new List<BucketOutcome>
        {
            new("High", BucketType.Country, WasAbandoned: false, AlbumsDiscovered: 10),
            new("LowA", BucketType.Country, WasAbandoned: false, AlbumsDiscovered: 2),
            new("LowB", BucketType.Country, WasAbandoned: false, AlbumsDiscovered: 2),
        };

        var report = DiscoveryRunReportCalculator.Calculate(outcomes);

        Assert.Equal(["LowA", "LowB"], report.LowestYieldBucketNames);
        Assert.Equal(2, report.LowestYieldCount);
    }
}
