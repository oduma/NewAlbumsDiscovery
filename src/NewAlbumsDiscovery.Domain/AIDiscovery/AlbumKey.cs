namespace NewAlbumsDiscovery.Domain.AIDiscovery;

/// <summary>
/// Normalized (Artist, Album) identity used for deduplication (docs/requirements/
/// FUNCTIONAL_REQUIREMENTS.md → Phase 10). Two albums are the same discovery if their artist and
/// album name match case-insensitively, ignoring leading/trailing whitespace.
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
