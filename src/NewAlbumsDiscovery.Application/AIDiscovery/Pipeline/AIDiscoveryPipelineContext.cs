using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Immutable, transient orchestration state threaded through the AIDiscovery pipeline's stages
/// (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 5 → Design Notes). Not a Domain concept —
/// this is an Application-layer coordination artifact with no business invariant of its own.
/// </summary>
public sealed record AIDiscoveryPipelineContext(IReadOnlyList<AggregatedBucket> SortedBuckets, int ProcessedBucketCount = 0);
