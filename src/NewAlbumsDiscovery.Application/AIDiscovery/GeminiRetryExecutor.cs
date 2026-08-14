namespace NewAlbumsDiscovery.Application.AIDiscovery;

/// <summary>
/// Shared retry/backoff policy for Gemini calls (docs/requirements/FUNCTIONAL_REQUIREMENTS.md →
/// Phase 10 → Decision 9), extracted from GenreExpansionPromptStep's Phase 9 retry loop so
/// DiscoveryQueryPromptStep doesn't duplicate it. Returns the terminal GeminiCallResult either
/// way (success, or the final failure once retries are exhausted / a permanent failure occurs) —
/// callers own what "failure" means for their step (abandonment message, fallback value, etc.).
/// </summary>
public sealed class GeminiRetryExecutor
{
    private readonly IGeminiClient _geminiClient;
    private readonly TimeProvider _timeProvider;

    public GeminiRetryExecutor(IGeminiClient geminiClient, TimeProvider timeProvider)
    {
        _geminiClient = geminiClient;
        _timeProvider = timeProvider;
    }

    public async Task<GeminiCallResult> ExecuteAsync(
        string prompt, IReadOnlyList<int> backoffSeconds, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var result = await _geminiClient.GenerateContentAsync(prompt, cancellationToken);

            if (result.IsSuccess)
            {
                return result;
            }

            if (result.IsTransientFailure && attempt < backoffSeconds.Count)
            {
                await Task.Delay(TimeSpan.FromSeconds(backoffSeconds[attempt]), _timeProvider, cancellationToken);
                continue;
            }

            return result;
        }
    }
}
