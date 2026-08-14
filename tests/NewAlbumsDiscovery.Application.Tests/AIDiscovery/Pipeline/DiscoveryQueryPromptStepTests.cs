using Microsoft.Extensions.Options;
using Moq;
using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;
using NewAlbumsDiscovery.Application.AIDiscovery.Prompts;
using NewAlbumsDiscovery.Application.Tests.TestSupport;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.Tests.AIDiscovery.Pipeline;

public class DiscoveryQueryPromptStepTests
{
    private static readonly DateTime AsOfUtc = new(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);

    private static DiscoveryQueryPromptStep CreateStep(
        Mock<IPromptTemplateProvider> templates,
        Mock<IDiscoveryNotifier> notifier,
        TimeProvider? timeProvider = null,
        int maxAlbumsPerQuery = 20,
        string instrumentalLanguage = "Instrumental")
    {
        var options = Options.Create(new AIDiscoveryOptions
        {
            MaxAlbumsPerQuery = maxAlbumsPerQuery,
            InstrumentalLanguage = instrumentalLanguage,
        });
        return new DiscoveryQueryPromptStep(
            templates.Object,
            new PromptRenderer(),
            new TimeframeFormatter(),
            notifier.Object,
            timeProvider ?? new RecordingTimeProvider(FixedUtcNow),
            options);
    }

    [Fact]
    public async Task ProcessAsync_WithInstrumentalCountryLanguageBucket_UsesCountryInstrumentalPromptAndOmitsGenres()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates
            .Setup(t => t.GetTemplateAsync("country-instrumental-prompt.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{{country}}|{{timeframe}}|{{maxAlbums}}|{{genres}}");
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(templates, notifier, maxAlbumsPerQuery: 20);
        var bucket = AggregatedBucket.Create("Romania/Instrumental", BucketType.CountryLanguage, "Romania", "Instrumental", null, 5, AsOfUtc);

        await step.ProcessAsync(bucket, new BucketProcessingState(), CancellationToken.None);

        notifier.Verify(
            n => n.NotifyPromptRenderedAsync(
                "--- PROMPT 2: DISCOVERY QUERY ---",
                "Romania|between 14-JUL-2026 and 13-AUG-2026|20|{{genres}}",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithInstrumentalCountryLanguageGenreBucket_UsesCountryInstrumentalGenresPromptAndSubstitutesGenreDirectly()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates
            .Setup(t => t.GetTemplateAsync("country-instrumental-genres-prompt.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{{country}}|{{genres}}|{{timeframe}}|{{maxAlbums}}");
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(templates, notifier, maxAlbumsPerQuery: 20);
        var bucket = AggregatedBucket.Create("Romania/Instrumental/Ambient", BucketType.CountryLanguageGenre, "Romania", "Instrumental", "Ambient", 5, AsOfUtc);

        await step.ProcessAsync(bucket, new BucketProcessingState(), CancellationToken.None);

        notifier.Verify(
            n => n.NotifyPromptRenderedAsync(
                "--- PROMPT 2: DISCOVERY QUERY ---",
                "Romania|Ambient|between 14-JUL-2026 and 13-AUG-2026|20",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithNonDefaultConfiguredInstrumentalLanguage_RoutesByConfiguredValue()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates
            .Setup(t => t.GetTemplateAsync("country-instrumental-prompt.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{{country}}");
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(templates, notifier, instrumentalLanguage: "NoVocals");
        var bucket = AggregatedBucket.Create("Romania/NoVocals", BucketType.CountryLanguage, "Romania", "NoVocals", null, 5, AsOfUtc);

        await step.ProcessAsync(bucket, new BucketProcessingState(), CancellationToken.None);

        notifier.Verify(
            n => n.NotifyPromptRenderedAsync("--- PROMPT 2: DISCOVERY QUERY ---", "Romania", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithCountryBucket_UsesCountryPromptAndOmitsLanguageAndGenres()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates
            .Setup(t => t.GetTemplateAsync("country-prompt.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{{country}}|{{timeframe}}|{{maxAlbums}}|{{language}}|{{genres}}");
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(templates, notifier, maxAlbumsPerQuery: 20);
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 15, AsOfUtc);

        await step.ProcessAsync(bucket, new BucketProcessingState(), CancellationToken.None);

        notifier.Verify(
            n => n.NotifyPromptRenderedAsync(
                "--- PROMPT 2: DISCOVERY QUERY ---",
                "Romania|between 14-JUL-2026 and 13-AUG-2026|20|{{language}}|{{genres}}",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithCountryLanguageBucket_UsesCountryLanguagePromptAndSubstitutesLanguage()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates
            .Setup(t => t.GetTemplateAsync("country-language-prompt.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{{country}}|{{language}}|{{timeframe}}|{{maxAlbums}}|{{genres}}");
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(templates, notifier, maxAlbumsPerQuery: 20);
        var bucket = AggregatedBucket.Create("Romania/Romanian", BucketType.CountryLanguage, "Romania", "Romanian", null, 15, AsOfUtc);

        await step.ProcessAsync(bucket, new BucketProcessingState(), CancellationToken.None);

        notifier.Verify(
            n => n.NotifyPromptRenderedAsync(
                "--- PROMPT 2: DISCOVERY QUERY ---",
                "Romania|Romanian|between 14-JUL-2026 and 13-AUG-2026|20|{{genres}}",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithCountryLanguageGenreBucket_UsesCountryLanguageGenresPromptAndSubstitutesResolvedGenres()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates
            .Setup(t => t.GetTemplateAsync("country-language-genres-prompt.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{{country}}|{{language}}|{{genres}}|{{timeframe}}|{{maxAlbums}}");
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(templates, notifier, maxAlbumsPerQuery: 20);
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);
        var state = new BucketProcessingState { ResolvedGenres = "Alt Pop, Bedroom Pop" };

        await step.ProcessAsync(bucket, state, CancellationToken.None);

        notifier.Verify(
            n => n.NotifyPromptRenderedAsync(
                "--- PROMPT 2: DISCOVERY QUERY ---",
                "Romania|Romanian|Alt Pop, Bedroom Pop|between 14-JUL-2026 and 13-AUG-2026|20",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_UsesMaxAlbumsPerQueryFromOptions()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates
            .Setup(t => t.GetTemplateAsync("country-prompt.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{{maxAlbums}}");
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(templates, notifier, maxAlbumsPerQuery: 42);
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 15, AsOfUtc);

        await step.ProcessAsync(bucket, new BucketProcessingState(), CancellationToken.None);

        notifier.Verify(
            n => n.NotifyPromptRenderedAsync("--- PROMPT 2: DISCOVERY QUERY ---", "42", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
