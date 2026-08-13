namespace NewAlbumsDiscovery.Domain.AIDiscovery;

/// <summary>
/// Value Object per docs/constitution/DDD-architecture.md §3 — immutable, equality by value.
/// Normalizes an (Artist, Album) pair (trim + lowercase-invariant) so dedup comparisons are
/// case- and whitespace-insensitive without repeating that logic at every call site.
/// </summary>
public readonly record struct AlbumKey
{
    public string NormalizedArtist { get; }
    public string NormalizedAlbum { get; }

    private AlbumKey(string normalizedArtist, string normalizedAlbum)
    {
        NormalizedArtist = normalizedArtist;
        NormalizedAlbum = normalizedAlbum;
    }

    public static AlbumKey From(string artist, string album)
        => new(Normalize(artist), Normalize(album));

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
