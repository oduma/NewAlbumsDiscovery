using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Application.AIDiscovery.Prompts;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

/// <summary>
/// Stage 2 step (docs/requirements/FUNCTIONAL_REQUIREMENTS.md -> Phase 7): renders and prints
/// Prompt 1 (genre-expansion-prompt.md) for CountryLanguageGenre buckets. No-ops for buckets
/// without a Genre and for Instrumental buckets.
/// </summary>
public sealed class GenreExpansionPromptStep : IBucketProcessingStep
{
    private const string TemplateFileName = "genre-expansion-prompt.md";
    private const string Header = "--- PROMPT 1: GENRE EXPANSION ---";

    private readonly IPromptTemplateProvider _templates;
    private readonly PromptRenderer _renderer;
    private readonly IDiscoveryNotifier _notifier;
    private readonly IOptions<AIDiscoveryOptions> _options;

    public GenreExpansionPromptStep(
        IPromptTemplateProvider templates,
        PromptRenderer renderer,
        IDiscoveryNotifier notifier,
        IOptions<AIDiscoveryOptions> options)
    {
        _templates = templates;
        _renderer = renderer;
        _notifier = notifier;
        _options = options;
    }

    public async Task ProcessAsync(AggregatedBucket bucket, CancellationToken cancellationToken)
    {
        if (bucket.Genre is null || bucket.IsInstrumental(_options.Value.InstrumentalLanguage))
        {
            return;
        }

        var template = await _templates.GetTemplateAsync(TemplateFileName, cancellationToken);
        var values = new Dictionary<string, string>
        {
            ["genre"] = bucket.Genre,
            ["country"] = bucket.Country,
            ["language"] = bucket.Language ?? string.Empty,
        };

        var rendered = _renderer.Render(template, values);
        await _notifier.NotifyPromptRenderedAsync(Header, rendered, cancellationToken);
    }
}
