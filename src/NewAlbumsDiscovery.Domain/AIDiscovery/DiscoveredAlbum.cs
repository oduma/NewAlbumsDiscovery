namespace NewAlbumsDiscovery.Domain.AIDiscovery;

/// <summary>
/// Entity per docs/constitution/DDD-architecture.md §3 — identity via Id, equality by Id alone.
/// Mirrors NewAlbumsDiscovery.Domain.MusicAggregator.AggregatedBucket's shape: the constructor
/// takes Id explicitly so EF Core can materialize existing rows through it; use <see cref="Create"/>
/// when producing a brand-new discovery so a fresh Id and Status.Pending are set for you
/// (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 10).
/// </summary>
public sealed class DiscoveredAlbum : IEquatable<DiscoveredAlbum>
{
    public Guid Id { get; }
    public Guid ReferenceBucketId { get; }
    public string Artist { get; }
    public string AlbumName { get; }
    public DiscoveredAlbumStatus Status { get; }
    public DateTime DiscoveredAtUtc { get; }

    public DiscoveredAlbum(
        Guid id,
        Guid referenceBucketId,
        string artist,
        string albumName,
        DiscoveredAlbumStatus status,
        DateTime discoveredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(artist))
        {
            throw new ArgumentException("Artist must not be null or empty.", nameof(artist));
        }

        if (string.IsNullOrWhiteSpace(albumName))
        {
            throw new ArgumentException("Album name must not be null or empty.", nameof(albumName));
        }

        Id = id;
        ReferenceBucketId = referenceBucketId;
        Artist = artist;
        AlbumName = albumName;
        Status = status;
        DiscoveredAtUtc = discoveredAtUtc;
    }

    public static DiscoveredAlbum Create(Guid referenceBucketId, string artist, string albumName, DateTime discoveredAtUtc)
        => new(Guid.NewGuid(), referenceBucketId, artist, albumName, DiscoveredAlbumStatus.Pending, discoveredAtUtc);

    public bool Equals(DiscoveredAlbum? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as DiscoveredAlbum);

    public override int GetHashCode() => Id.GetHashCode();
}
