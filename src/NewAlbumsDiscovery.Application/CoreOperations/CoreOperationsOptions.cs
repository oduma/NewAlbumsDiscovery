namespace NewAlbumsDiscovery.Application.CoreOperations;

/// <summary>
/// Bound from configuration section "NewAlbumsDiscovery:CoreOperations"
/// (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 5).
/// </summary>
public sealed class CoreOperationsOptions
{
    public int TriggerDelaySeconds { get; set; } = 30;
}
