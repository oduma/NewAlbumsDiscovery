namespace NewAlbumsDiscovery.Domain.AIDiscovery;

/// <summary>
/// Selects the subset of candidate (Artist, Album) pairs not already represented in
/// <paramref name="existingKeys"/> (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 10).
/// Mutates <paramref name="existingKeys"/> in place by adding each accepted candidate's key, so
/// callers can thread the same set across successive calls to catch duplicates both within a
/// batch and across batches without a second lookup structure.
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
