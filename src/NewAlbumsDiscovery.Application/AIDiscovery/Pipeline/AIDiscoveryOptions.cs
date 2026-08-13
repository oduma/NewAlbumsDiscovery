namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Bound from configuration section "NewAlbumsDiscovery:AIDiscovery"
/// (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 5).
/// </summary>
public sealed class AIDiscoveryOptions
{
    public int InterBucketDelaySeconds { get; set; } = 10;
}
