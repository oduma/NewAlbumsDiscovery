using System.Globalization;
using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Application.AIDiscovery.Prompts;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Stage 2 step (docs/requirements/FUNCTIONAL_REQUIREMENTS.md -> Phase 7): renders and prints
/// Prompt 2 for every non-instrumental bucket, selecting the template by BucketType. Genres are
/// always substituted with the literal "TBD" for CountryLanguageGenre buckets this phase - no
/// real genre-expansion chaining yet.
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

    public async Task ProcessAsync(AggregatedBucket bucket, CancellationToken cancellationToken)
    {
        if (bucket.IsInstrumental)
        {
            return;
        }

        var templateFileName = TemplatesByBucketType[bucket.BucketType];
        var template = await _templates.GetTemplateAsync(templateFileName, cancellationToken);

        var values = new Dictionary<string, string>
        {
            ["country"] = bucket.Country,
            ["timeframe"] = _timeframeFormatter.Format(_timeProvider.GetUtcNow()),
            ["maxAlbums"] = _options.Value.MaxAlbumsPerQuery.ToString(CultureInfo.InvariantCulture),
        };

        if (bucket.Language is not null)
        {
            values["language"] = bucket.Language;
        }

        if (bucket.Genre is not null)
        {
            values["genres"] = "TBD";
        }

        var rendered = _renderer.Render(template, values);
        await _notifier.NotifyPromptRenderedAsync(Header, rendered, cancellationToken);
    }
}
