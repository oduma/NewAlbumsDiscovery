namespace NewAlbumsDiscovery.Domain.MusicAggregator.Filtering;

/// <summary>
/// Phase 6 Rule B: drops any bucket whose Country is neither a known country nor a known
/// continent in the master data (e.g. "Unknown", "Global/Various", or an arbitrary string).
/// </summary>
public sealed class InvalidCountryDeletionRule : IBucketFilterRule
{
    public IReadOnlyList<AggregatedBucket> Apply(IReadOnlyList<AggregatedBucket> buckets, CountryMasterData masterData)
        => buckets.Where(b => masterData.IsKnownCountry(b.Country) || masterData.IsContinentName(b.Country)).ToList();
}
