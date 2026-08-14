using System.Text.Json;
using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Application.AIDiscovery.Prompts;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;

public sealed class GenreExpansionPromptStep : IBucketProcessingStep
{
    private const string TemplateFileName = "genre-expansion-prompt.md";

    private readonly IPromptTemplateProvider _templates;
    private readonly PromptRenderer _renderer;
    private readonly IDiscoveryNotifier _notifier;
    private readonly IGeminiClient _geminiClient;
    private readonly IOptions<AIDiscoveryOptions> _aiDiscoveryOptions;
    private readonly IOptions<GeminiOptions> _geminiOptions;
    private readonly TimeProvider _timeProvider;

    public GenreExpansionPromptStep(
        IPromptTemplateProvider templates,
        PromptRenderer renderer,
        IDiscoveryNotifier notifier,
        IGeminiClient geminiClient,
        IOptions<AIDiscoveryOptions> aiDiscoveryOptions,
        IOptions<GeminiOptions> geminiOptions,
        TimeProvider timeProvider)
    {
        _templates = templates;
        _renderer = renderer;
        _notifier = notifier;
        _geminiClient = geminiClient;
        _aiDiscoveryOptions = aiDiscoveryOptions;
        _geminiOptions = geminiOptions;
        _timeProvider = timeProvider;
    }

    public async Task ProcessAsync(AggregatedBucket bucket, BucketProcessingState state, CancellationToken cancellationToken)
    {
        if (bucket.Genre is null || bucket.IsInstrumental(_aiDiscoveryOptions.Value.InstrumentalLanguage))
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
        var prompt = _renderer.Render(template, values);

        var backoffs = _geminiOptions.Value.RetryBackoffSeconds;

        for (var attempt = 0; ; attempt++)
        {
            var result = await _geminiClient.GenerateContentAsync(prompt, cancellationToken);

            if (result.IsSuccess)
            {
                state.ResolvedGenres = ResolveGenres(result.ResponseText!, bucket.Genre);
                return;
            }

            if (result.IsTransientFailure && attempt < backoffs.Length)
            {
                await Task.Delay(TimeSpan.FromSeconds(backoffs[attempt]), _timeProvider, cancellationToken);
                continue;
            }

            await _notifier.NotifyBucketAbandonedAsync(
                bucket.BucketName, bucket.TrackCount, result.ErrorMessage ?? "Gemini API call failed.", cancellationToken);
            state.Abandon();
            return;
        }
    }

    private static string ResolveGenres(string responseText, string fallbackGenre)
    {
        try
        {
            var genres = JsonSerializer.Deserialize<string[]>(responseText);
            return genres is { Length: > 0 } ? string.Join(", ", genres) : fallbackGenre;
        }
        catch (JsonException)
        {
            return fallbackGenre;
        }
    }
}
