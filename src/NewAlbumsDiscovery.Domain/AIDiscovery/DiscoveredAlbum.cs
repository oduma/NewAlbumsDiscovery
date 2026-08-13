namespace NewAlbumsDiscovery.Domain.AIDiscovery;

/// <summary>
/// Entity per docs/constitution/DDD-architecture.md §3 — identity via Id, equality by Id alone.
/// Mirrors NewAlbumsDiscovery.Domain.MusicAggregator.AggregatedBucket's shape: the constructor
/// takes Id explicitly (so EF Core can materialize existing rows through it) while
/// <see cref="Create"/> generates a fresh Id for brand-new discoveries.
/// </summary>
public sealed class DiscoveredAlbum : IEquatable<DiscoveredAlbum>
{
    public Guid Id { get; }
    public string Artist { get; }
    public string Album { get; }
    public string Country { get; }
    public string? Language { get; }
    public string? Genre { get; }
    public DateTime DiscoveredAtUtc { get; }

    public DiscoveredAlbum(
        Guid id,
        string artist,
        string album,
        string country,
        string? language,
        string? genre,
        DateTime discoveredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(artist))
        {
            throw new ArgumentException("Artist must not be null or empty.", nameof(artist));
        }

        if (string.IsNullOrWhiteSpace(album))
        {
            throw new ArgumentException("Album must not be null or empty.", nameof(album));
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            throw new ArgumentException("Country must not be null or empty.", nameof(country));
        }

        Id = id;
        Artist = artist;
        Album = album;
        Country = country;
        Language = language;
        Genre = genre;
        DiscoveredAtUtc = discoveredAtUtc;
    }

    public static DiscoveredAlbum Create(
        string artist,
        string album,
        string country,
        string? language,
        string? genre,
        DateTime discoveredAtUtc)
        => new(Guid.NewGuid(), artist, album, country, language, genre, discoveredAtUtc);

    public bool Equals(DiscoveredAlbum? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as DiscoveredAlbum);

    public override int GetHashCode() => Id.GetHashCode();
}
