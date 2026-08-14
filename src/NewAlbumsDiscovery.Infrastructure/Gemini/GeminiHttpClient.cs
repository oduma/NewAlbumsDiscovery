using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Application.AIDiscovery;

namespace NewAlbumsDiscovery.Infrastructure.Gemini;

/// <summary>
/// Raw-HttpClient adapter for the Gemini REST generateContent endpoint
/// (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 9). Single-attempt only - the caller
/// (GenreExpansionPromptStep) owns the retry/backoff policy. Classifies every failure as transient
/// (worth retrying) or permanent (400/401/403, or an unparsable 200 OK body).
/// </summary>
public sealed class GeminiHttpClient : IGeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<GeminiOptions> _options;
    private readonly IConfiguration _configuration;

    public GeminiHttpClient(HttpClient httpClient, IOptions<GeminiOptions> options, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _options = options;
        _configuration = configuration;
    }

    public async Task<GeminiCallResult> GenerateContentAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Value.Model}:generateContent";
        var requestBody = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(requestBody),
        };
        request.Headers.Add("x-goog-api-key", _configuration["NewAlbumsDiscovery:GeminiApiKey"]);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return GeminiCallResult.Transient($"Network error calling Gemini API: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return GeminiCallResult.Transient($"Timeout calling Gemini API: {ex.Message}");
        }

        using (response)
        {
            var statusCode = (int)response.StatusCode;

            if (statusCode is 400 or 401 or 403)
            {
                return GeminiCallResult.Permanent($"Gemini API returned {statusCode} {response.StatusCode}.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return GeminiCallResult.Transient($"Gemini API returned {statusCode} {response.StatusCode}.");
            }

            var text = await ExtractResponseTextAsync(response, cancellationToken);
            return text is null
                ? GeminiCallResult.Permanent("Gemini API returned 200 OK with an unexpected response shape.")
                : GeminiCallResult.Success(text);
        }
    }

    private static async Task<string?> ExtractResponseTextAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return document.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or IndexOutOfRangeException or KeyNotFoundException)
        {
            return null;
        }
    }
}
