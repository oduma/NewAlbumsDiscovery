namespace NewAlbumsDiscovery.Domain.MusicAggregator.Filtering;

/// <summary>
/// Phase 6 Rule A: drops a continent-level bucket (e.g. "Europe") whenever any other bucket -
/// regardless of BucketType - resolves via master data to a specific country within that
/// continent. A bucket named after an actual country (e.g. "Australia") is never a candidate for
/// elimination in the first place, since its name never equals a continent name.
/// </summary>
public sealed class ContinentFallbackEliminationRule : IBucketFilterRule
{
    public IReadOnlyList<AggregatedBucket> Apply(IReadOnlyList<AggregatedBucket> buckets, CountryMasterData masterData)
    {
        bool HasSpecificCountryEvidence(string continent) => buckets.Any(b =>
            !masterData.IsContinentName(b.Country)
            && masterData.TryGetContinent(b.Country, out var bucketContinent)
            && string.Equals(bucketContinent, continent, StringComparison.OrdinalIgnoreCase));

        return buckets
            .Where(b => !masterData.IsContinentName(b.Country) || !HasSpecificCountryEvidence(b.Country))
            .ToList();
    }
}
