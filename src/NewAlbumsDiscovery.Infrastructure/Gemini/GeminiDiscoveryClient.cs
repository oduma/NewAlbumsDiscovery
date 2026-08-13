using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Application.AIDiscovery;

namespace NewAlbumsDiscovery.Infrastructure.Gemini;

/// <summary>
/// Template selection, {{placeholder}} rendering, JSON parsing, and the retry/backoff loop over
/// IGeminiApiClient. Both malformed-JSON responses and rate-limit-shaped exceptions share the same
/// retry path (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 4, decision #6): after
/// MaxRetryAttempts, the bucket's failure is logged and an empty candidate list is returned rather
/// than throwing, so one bad bucket never aborts the whole DiscoverAlbumsCommand run.
/// </summary>
public sealed class GeminiDiscoveryClient : IGeminiDiscoveryClient
{
    private const string CountryTemplateResource = "NewAlbumsDiscovery.Infrastructure.Gemini.Prompts.country-prompt.md";
    private const string CountryLanguageTemplateResource = "NewAlbumsDiscovery.Infrastructure.Gemini.Prompts.country-language-prompt.md";
    private const string CountryLanguageGenresTemplateResource = "NewAlbumsDiscovery.Infrastructure.Gemini.Prompts.country-language-genres-prompt.md";

    private static readonly Lazy<string> CountryTemplate = new(() => LoadTemplate(CountryTemplateResource));
    private static readonly Lazy<string> CountryLanguageTemplate = new(() => LoadTemplate(CountryLanguageTemplateResource));
    private static readonly Lazy<string> CountryLanguageGenresTemplate = new(() => LoadTemplate(CountryLanguageGenresTemplateResource));

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IGeminiApiClient _apiClient;
    private readonly IOptions<GeminiOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GeminiDiscoveryClient> _logger;

    public GeminiDiscoveryClient(
        IGeminiApiClient apiClient,
        IOptions<GeminiOptions> options,
        TimeProvider timeProvider,
        ILogger<GeminiDiscoveryClient> logger)
    {
        _apiClient = apiClient;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CandidateAlbum>> DiscoverAsync(DiscoveryPromptRequest request, CancellationToken cancellationToken)
    {
        var prompt = RenderPrompt(request);
        var maxAttempts = _options.Value.MaxRetryAttempts;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var rawResponse = await _apiClient.GenerateContentAsync(prompt, cancellationToken);
                return ParseCandidates(rawResponse);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                if (attempt == maxAttempts)
                {
                    _logger.LogError(
                        ex,
                        "Gemini discovery failed for Country={Country}, Language={Language}, Genre={Genre} after {Attempts} attempt(s).",
                        request.Country, request.Language, request.Genre, attempt);
                    return [];
                }

                var delaySeconds = _options.Value.InitialBackoffSeconds * Math.Pow(2, attempt - 1);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), _timeProvider, cancellationToken);
            }
        }

        return [];
    }

    public static string RenderPrompt(DiscoveryPromptRequest request)
    {
        var rendered = SelectTemplate(request)
            .Replace("{{country}}", request.Country)
            .Replace("{{timeframe}}", request.Timeframe)
            .Replace("{{maxAlbums}}", request.MaxAlbums.ToString());

        if (request.Language is not null)
        {
            rendered = rendered.Replace("{{language}}", request.Language);
        }

        if (request.Genre is not null)
        {
            rendered = rendered.Replace("{{genres}}", request.Genre);
        }

        return rendered;
    }

    private static string SelectTemplate(DiscoveryPromptRequest request)
    {
        if (request.Genre is not null)
        {
            return CountryLanguageGenresTemplate.Value;
        }

        return request.Language is not null ? CountryLanguageTemplate.Value : CountryTemplate.Value;
    }

    private static IReadOnlyList<CandidateAlbum> ParseCandidates(string rawResponse)
    {
        var dtos = JsonSerializer.Deserialize<List<GeminiAlbumDto>>(rawResponse, JsonOptions)
            ?? throw new JsonException("Gemini response deserialized to null.");

        return dtos.Select(dto => new CandidateAlbum(dto.Artist, dto.Album)).ToList();
    }

    private static string LoadTemplate(string resourceName)
    {
        var assembly = typeof(GeminiDiscoveryClient).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded prompt template '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed record GeminiAlbumDto(string Artist, string Album);
}
