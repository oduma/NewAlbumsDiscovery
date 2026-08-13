namespace NewAlbumsDiscovery.Domain.MusicAggregator;

/// <summary>
/// Pure, deterministic pre-pass that rewrites synonym country names (e.g. "United States") to
/// their canonical form (e.g. "USA") before <see cref="BucketAggregatorEngine"/> groups tracks by
/// country, so synonym variants contribute to the same threshold bucket instead of splintering.
/// </summary>
public sealed class CountryNormalizer
{
    public IReadOnlyList<LovedTrackPreferences> Normalize(
        IReadOnlyList<LovedTrackPreferences> tracks,
        IReadOnlyDictionary<string, string> synonyms)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        ArgumentNullException.ThrowIfNull(synonyms);

        var caseInsensitiveSynonyms = new Dictionary<string, string>(synonyms, StringComparer.OrdinalIgnoreCase);

        return tracks
            .Select(track => caseInsensitiveSynonyms.TryGetValue(track.Country, out var canonical)
                ? new LovedTrackPreferences(canonical, track.Languages, track.Genres)
                : track)
            .ToList();
    }
}
