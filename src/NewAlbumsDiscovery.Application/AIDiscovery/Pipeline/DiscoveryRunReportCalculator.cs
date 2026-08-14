using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Pure computation of the Stage 3 report from a run's BucketOutcomes (docs/requirements/
/// FUNCTIONAL_REQUIREMENTS.md → Phase 10 → §4 and Design Notes). Level 1/2/3 map directly onto
/// BucketType (Country/CountryLanguage/CountryLanguageGenre, per Phase 7 Decision 3). Eligibility
/// for every average and both highest/lowest yield metrics is the same filter: not abandoned and
/// AlbumsDiscovered &gt; 0.
/// </summary>
public static class DiscoveryRunReportCalculator
{
    public static DiscoveryRunReport Calculate(IReadOnlyList<BucketOutcome> outcomes)
    {
        var eligible = outcomes.Where(o => !o.WasAbandoned && o.AlbumsDiscovered > 0).ToList();
        var highestCount = eligible.Count == 0 ? 0 : eligible.Max(o => o.AlbumsDiscovered);
        var lowestCount = eligible.Count == 0 ? 0 : eligible.Min(o => o.AlbumsDiscovered);

        return new DiscoveryRunReport(
            TotalBuckets: outcomes.Count,
            BucketsSkipped: outcomes.Count(o => o.WasAbandoned),
            EmptyBuckets: outcomes.Count(o => !o.WasAbandoned && o.AlbumsDiscovered == 0),
            TotalAlbumsDiscovered: outcomes.Sum(o => o.AlbumsDiscovered),
            AverageAlbumsPerBucket: Average(eligible),
            AverageAlbumsPerLevel1: Average(eligible.Where(o => o.BucketType == BucketType.Country).ToList()),
            AverageAlbumsPerLevel2: Average(eligible.Where(o => o.BucketType == BucketType.CountryLanguage).ToList()),
            AverageAlbumsPerLevel3: Average(eligible.Where(o => o.BucketType == BucketType.CountryLanguageGenre).ToList()),
            HighestYieldBucketNames: YieldNames(eligible, highestCount),
            HighestYieldCount: highestCount,
            LowestYieldBucketNames: YieldNames(eligible, lowestCount),
            LowestYieldCount: lowestCount);
    }

    private static double? Average(IReadOnlyList<BucketOutcome> eligible)
        => eligible.Count == 0 ? null : eligible.Average(o => o.AlbumsDiscovered);

    private static IReadOnlyList<string> YieldNames(IReadOnlyList<BucketOutcome> eligible, int count)
        => eligible.Count == 0 ? [] : eligible.Where(o => o.AlbumsDiscovered == count).Select(o => o.BucketName).ToList();
}
