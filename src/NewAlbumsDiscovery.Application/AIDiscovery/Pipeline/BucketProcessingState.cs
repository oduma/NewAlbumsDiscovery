namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Mutable, per-bucket coordination state threaded through one bucket's step loop inside
/// BucketProcessingStage. Not a Domain concept - an Application-only orchestration artifact,
/// recreated fresh for every bucket (docs/requirements/FUNCTIONAL_REQUIREMENTS.md -> Phase 9).
/// </summary>
public sealed class BucketProcessingState
{
    public string? ResolvedGenres { get; set; }

    public bool IsAbandoned { get; private set; }

    public void Abandon() => IsAbandoned = true;

    /// <summary>Set by DiscoveryQueryPromptStep, consumed by AlbumPersistenceStep (Phase 10).</summary>
    public IReadOnlyList<(string Artist, string Album)> DiscoveredCandidates { get; set; } = [];

    /// <summary>Set by AlbumPersistenceStep, read by BucketProcessingStage to build this bucket's BucketOutcome (Phase 10).</summary>
    public int PersistedAlbumCount { get; set; }
}
