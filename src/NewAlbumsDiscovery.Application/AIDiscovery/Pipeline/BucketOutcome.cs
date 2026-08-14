using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Per-bucket result recorded by BucketProcessingStage for every bucket it visits (abandoned or
/// not), feeding DiscoveryRunReportCalculator (docs/requirements/FUNCTIONAL_REQUIREMENTS.md →
/// Phase 10).
/// </summary>
public sealed record BucketOutcome(string BucketName, BucketType BucketType, bool WasAbandoned, int AlbumsDiscovered);
