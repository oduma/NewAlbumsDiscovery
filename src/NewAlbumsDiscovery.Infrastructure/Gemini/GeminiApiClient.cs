using Mscc.GenerativeAI;

namespace NewAlbumsDiscovery.Infrastructure.Gemini;

/// <summary>
/// No dedicated unit test (see IGeminiApiClient) — exercised implicitly whenever the app runs
/// against the real Gemini API.
/// </summary>
public sealed class GeminiApiClient : IGeminiApiClient
{
    private readonly GenerativeModel _model;

    public GeminiApiClient(string apiKey, string model)
    {
        var googleAI = new GoogleAI(apiKey: apiKey);
        _model = googleAI.GenerativeModel(model: model);
    }

    public async Task<string> GenerateContentAsync(string prompt, CancellationToken cancellationToken)
    {
        var response = await _model.GenerateContent(prompt, cancellationToken: cancellationToken);
        return response.Text ?? string.Empty;
    }
}
