namespace NewAlbumsDiscovery.Domain.AIDiscovery;

/// <summary>
/// Pure domain service implementing the global dedup rule (docs/requirements/FUNCTIONAL_REQUIREMENTS.md
/// → Phase 4, decision #4): an (Artist, Album) pair already known is never selected again, and
/// within a single candidate batch only the first occurrence of a duplicate is kept.
/// <paramref name="existingKeys"/> is mutated in place (each selected candidate's key is added to
/// it) so callers can feed the same set through successive calls (e.g. once per bucket) and get
/// cross-call dedup for free, without re-deriving keys from what was just selected.
/// </summary>
public static class AlbumDeduplicator
{
    public static IReadOnlyList<(string Artist, string Album)> SelectNew(
        IReadOnlyList<(string Artist, string Album)> candidates,
        ISet<AlbumKey> existingKeys)
    {
        var result = new List<(string Artist, string Album)>();

        foreach (var candidate in candidates)
        {
            var key = AlbumKey.From(candidate.Artist, candidate.Album);
            if (existingKeys.Add(key))
            {
                result.Add(candidate);
            }
        }

        return result;
    }
}
