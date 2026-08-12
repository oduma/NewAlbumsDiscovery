namespace NewAlbumsDiscovery.Domain.MusicAggregator;

/// <summary>
/// The three cascade thresholds <see cref="BucketAggregatorEngine"/> operates on. Domain must not
/// reference Application's IOptions-bound AggregatorSettings (dependency only flows the other way),
/// so the Application-layer command handler maps AggregatorSettings into this value object before
/// calling the engine.
/// </summary>
public sealed record AggregationThresholds
{
    public int CountryRegionThreshold { get; }
    public int CountryRegionLanguageThreshold { get; }
    public int MinimumBucketThreshold { get; }

    public AggregationThresholds(int countryRegionThreshold, int countryRegionLanguageThreshold, int minimumBucketThreshold)
    {
        if (countryRegionThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(countryRegionThreshold), countryRegionThreshold, "Must be positive.");
        }

        if (countryRegionLanguageThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(countryRegionLanguageThreshold), countryRegionLanguageThreshold, "Must be positive.");
        }

        if (minimumBucketThreshold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumBucketThreshold), minimumBucketThreshold, "Must not be negative.");
        }

        CountryRegionThreshold = countryRegionThreshold;
        CountryRegionLanguageThreshold = countryRegionLanguageThreshold;
        MinimumBucketThreshold = minimumBucketThreshold;
    }
}
