namespace NewAlbumsDiscovery.Domain.MusicAggregator;

/// <summary>
/// Entity per docs/constitution/DDD-architecture.md §3 — identity via Id, equality by Id alone.
/// The constructor takes Id explicitly (rather than always generating one) so EF Core can
/// materialize existing rows through the same constructor; use <see cref="Create"/> when producing
/// a brand-new bucket so a fresh Id is generated for you.
/// </summary>
public sealed class AggregatedBucket : IEquatable<AggregatedBucket>
{
    public Guid Id { get; }
    public string BucketName { get; }
    public BucketType BucketType { get; }
    public string Country { get; }
    public string? Language { get; }
    public string? Genre { get; }
    public int TrackCount { get; }
    public DateTime CreatedAtUtc { get; }

    public AggregatedBucket(
        Guid id,
        string bucketName,
        BucketType bucketType,
        string country,
        string? language,
        string? genre,
        int trackCount,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            throw new ArgumentException("Bucket name must not be null or empty.", nameof(bucketName));
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            throw new ArgumentException("Country must not be null or empty.", nameof(country));
        }

        if (trackCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trackCount), trackCount, "Track count must not be negative.");
        }

        Id = id;
        BucketName = bucketName;
        BucketType = bucketType;
        Country = country;
        Language = language;
        Genre = genre;
        TrackCount = trackCount;
        CreatedAtUtc = createdAtUtc;
    }

    public static AggregatedBucket Create(
        string bucketName,
        BucketType bucketType,
        string country,
        string? language,
        string? genre,
        int trackCount,
        DateTime asOfUtc)
        => new(Guid.NewGuid(), bucketName, bucketType, country, language, genre, trackCount, asOfUtc);

    public bool Equals(AggregatedBucket? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as AggregatedBucket);

    public override int GetHashCode() => Id.GetHashCode();
}
