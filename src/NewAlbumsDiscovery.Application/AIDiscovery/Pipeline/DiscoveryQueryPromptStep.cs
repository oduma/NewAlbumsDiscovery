using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Application.AIDiscovery.Prompts;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Stage 2 step (docs/requirements/FUNCTIONAL_REQUIREMENTS.md -> Phase 7, extended in Phase 8/9,
/// wired to the real Gemini API in Phase 10): renders Prompt 2 for every bucket, selecting the
/// template by BucketType and by whether the bucket is instrumental, then sends it to Gemini
/// (never printed to the console — Phase 10 §2) via the shared GeminiRetryExecutor. A successful
/// response is parsed into candidate (Artist, Album) pairs onto BucketProcessingState for
/// AlbumPersistenceStep to dedup/persist; a malformed response yields zero candidates without
/// retrying or abandoning (Phase 10 Decision 6); an exhausted-retries or permanent failure
/// abandons the bucket the same way GenreExpansionPromptStep does.
/// </summary>
public sealed class DiscoveryQueryPromptStep : IBucketProcessingStep
{
    private static readonly JsonSerializerOptions AlbumJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly IReadOnlyDictionary<BucketType, string> TemplatesByBucketType = new Dictionary<BucketType, string>
    {
        [BucketType.Country] = "country-prompt.md",
        [BucketType.CountryLanguage] = "country-language-prompt.md",
        [BucketType.CountryLanguageGenre] = "country-language-genres-prompt.md",
    };

    private static readonly IReadOnlyDictionary<BucketType, string> InstrumentalTemplatesByBucketType = new Dictionary<BucketType, string>
    {
        [BucketType.CountryLanguage] = "country-instrumental-prompt.md",
        [BucketType.CountryLanguageGenre] = "country-instrumental-genres-prompt.md",
    };

    private readonly IPromptTemplateProvider _templates;
    private readonly PromptRenderer _renderer;
    private readonly TimeframeFormatter _timeframeFormatter;
    private readonly IDiscoveryNotifier _notifier;
    private readonly TimeProvider _timeProvider;
    private readonly GeminiRetryExecutor _retryExecutor;
    private readonly IOptions<AIDiscoveryOptions> _options;
    private readonly IOptions<GeminiOptions> _geminiOptions;

    public DiscoveryQueryPromptStep(
        IPromptTemplateProvider templates,
        PromptRenderer renderer,
        TimeframeFormatter timeframeFormatter,
        IDiscoveryNotifier notifier,
        TimeProvider timeProvider,
        GeminiRetryExecutor retryExecutor,
        IOptions<AIDiscoveryOptions> options,
        IOptions<GeminiOptions> geminiOptions)
    {
        _templates = templates;
        _renderer = renderer;
        _timeframeFormatter = timeframeFormatter;
        _notifier = notifier;
        _timeProvider = timeProvider;
        _retryExecutor = retryExecutor;
        _options = options;
        _geminiOptions = geminiOptions;
    }

    public async Task ProcessAsync(AggregatedBucket bucket, BucketProcessingState state, ISet<AlbumKey> existingAlbumKeys, CancellationToken cancellationToken)
    {
        var isInstrumental = bucket.IsInstrumental(_options.Value.InstrumentalLanguage);

        var templateFileName = isInstrumental
            ? InstrumentalTemplatesByBucketType[bucket.BucketType]
            : TemplatesByBucketType[bucket.BucketType];
        var template = await _templates.GetTemplateAsync(templateFileName, cancellationToken);

        var values = new Dictionary<string, string>
        {
            ["country"] = bucket.Country,
            ["timeframe"] = _timeframeFormatter.Format(_timeProvider.GetUtcNow()),
            ["maxAlbums"] = _options.Value.MaxAlbumsPerQuery.ToString(CultureInfo.InvariantCulture),
        };

        if (!isInstrumental && bucket.Language is not null)
        {
            values["language"] = bucket.Language;
        }

        if (bucket.Genre is not null)
        {
            values["genres"] = isInstrumental ? bucket.Genre : state.ResolvedGenres!;
        }

        var rendered = _renderer.Render(template, values);

        var result = await _retryExecutor.ExecuteAsync(rendered, _geminiOptions.Value.RetryBackoffSeconds, cancellationToken);

        if (result.IsSuccess)
        {
            state.DiscoveredCandidates = ParseCandidates(result.ResponseText!);
            return;
        }

        await _notifier.NotifyBucketAbandonedAsync(
            bucket.BucketName, bucket.TrackCount, result.ErrorMessage ?? "Gemini API call failed.", cancellationToken);
        state.Abandon();
    }

    private static IReadOnlyList<(string Artist, string Album)> ParseCandidates(string responseText)
    {
        try
        {
            var dtos = JsonSerializer.Deserialize<List<GeminiAlbumDto>>(responseText, AlbumJsonOptions);
            return dtos?.Select(dto => (dto.Artist, dto.Album)).ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record GeminiAlbumDto(string Artist, string Album);
}
