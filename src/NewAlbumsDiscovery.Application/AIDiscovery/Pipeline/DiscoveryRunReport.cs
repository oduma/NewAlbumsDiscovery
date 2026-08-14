namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// The 10-metric Stage 3 report (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 10 → §4).
/// Average*/HighestYield*/LowestYield* are computed only over eligible buckets (not abandoned,
/// AlbumsDiscovered &gt; 0) — null averages and empty yield-name lists mean no bucket was eligible.
/// </summary>
public sealed record DiscoveryRunReport(
    int TotalBuckets,
    int BucketsSkipped,
    int EmptyBuckets,
    int TotalAlbumsDiscovered,
    double? AverageAlbumsPerBucket,
    double? AverageAlbumsPerLevel1,
    double? AverageAlbumsPerLevel2,
    double? AverageAlbumsPerLevel3,
    IReadOnlyList<string> HighestYieldBucketNames,
    int HighestYieldCount,
    IReadOnlyList<string> LowestYieldBucketNames,
    int LowestYieldCount);
