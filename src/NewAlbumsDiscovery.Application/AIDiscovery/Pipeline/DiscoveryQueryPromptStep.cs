using System.Globalization;
using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Application.AIDiscovery.Prompts;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Stage 2 step (docs/requirements/FUNCTIONAL_REQUIREMENTS.md -> Phase 7, extended in Phase 8/9):
/// renders and prints Prompt 2 for every bucket, selecting the template by BucketType and by
/// whether the bucket is instrumental. For non-instrumental CountryLanguageGenre buckets, genres
/// are substituted with the resolved genre string GenreExpansionPromptStep placed on the shared
/// BucketProcessingState (Phase 9) - this step never calls Gemini itself. For instrumental
/// CountryLanguageGenre buckets, genres substitute the bucket's own Genre directly, since there is
/// no genre-expansion pass to chain from.
/// </summary>
public sealed class DiscoveryQueryPromptStep : IBucketProcessingStep
{
    private const string Header = "--- PROMPT 2: DISCOVERY QUERY ---";

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
    private readonly IOptions<AIDiscoveryOptions> _options;

    public DiscoveryQueryPromptStep(
        IPromptTemplateProvider templates,
        PromptRenderer renderer,
        TimeframeFormatter timeframeFormatter,
        IDiscoveryNotifier notifier,
        TimeProvider timeProvider,
        IOptions<AIDiscoveryOptions> options)
    {
        _templates = templates;
        _renderer = renderer;
        _timeframeFormatter = timeframeFormatter;
        _notifier = notifier;
        _timeProvider = timeProvider;
        _options = options;
    }

    public async Task ProcessAsync(AggregatedBucket bucket, BucketProcessingState state, CancellationToken cancellationToken)
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
        await _notifier.NotifyPromptRenderedAsync(Header, rendered, cancellationToken);
    }
}
