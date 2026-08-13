namespace NewAlbumsDiscovery.Domain.MusicAggregator.Filtering;

public interface IBucketFilterRule
{
    IReadOnlyList<AggregatedBucket> Apply(IReadOnlyList<AggregatedBucket> buckets, CountryMasterData masterData);
}
