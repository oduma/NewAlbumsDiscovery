namespace NewAlbumsDiscovery.Application.MusicAggregator;

/// <summary>
/// Bound from configuration section "NewAlbumsDiscovery:Aggregator" (docs/requirements/phase3-requirements.md §5).
/// Defaults match the confirmed values (2026-08-12): CountryRegionThreshold=10, CountryRegionLanguageThreshold=10,
/// MinimumBucketThreshold=2.
/// </summary>
public sealed class AggregatorSettings
{
    public int CountryRegionThreshold { get; set; } = 10;
    public int CountryRegionLanguageThreshold { get; set; } = 10;
    public int MinimumBucketThreshold { get; set; } = 2;
}
