namespace NewAlbumsDiscovery.Application.AIDiscovery;

public interface IGeminiClient
{
    Task<GeminiCallResult> GenerateContentAsync(string prompt, CancellationToken cancellationToken);
}
