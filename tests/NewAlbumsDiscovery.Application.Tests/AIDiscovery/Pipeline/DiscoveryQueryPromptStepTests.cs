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
        int maxAlbumsPerQuery = 20)
    {
        var options = Options.Create(new AIDiscoveryOptions { MaxAlbumsPerQuery = maxAlbumsPerQuery });
        return new DiscoveryQueryPromptStep(
            templates.Object,
            new PromptRenderer(),
            new TimeframeFormatter(),
            notifier.Object,
            timeProvider ?? new RecordingTimeProvider(FixedUtcNow),
            options);
    }

    [Fact]
    public async Task ProcessAsync_WithInstrumentalBucket_NeverRequestsTemplateOrNotifies()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(templates, notifier);
        var bucket = AggregatedBucket.Create("Romania/Instrumental", BucketType.CountryLanguage, "Romania", "Instrumental", null, 5, AsOfUtc);

        await step.ProcessAsync(bucket, CancellationToken.None);

        templates.Verify(t => t.GetTemplateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        notifier.Verify(n => n.NotifyPromptRenderedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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

        await step.ProcessAsync(bucket, CancellationToken.None);

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

        await step.ProcessAsync(bucket, CancellationToken.None);

        notifier.Verify(
            n => n.NotifyPromptRenderedAsync(
                "--- PROMPT 2: DISCOVERY QUERY ---",
                "Romania|Romanian|between 14-JUL-2026 and 13-AUG-2026|20|{{genres}}",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithCountryLanguageGenreBucket_UsesCountryLanguageGenresPromptAndSubstitutesGenresAsTbd()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates
            .Setup(t => t.GetTemplateAsync("country-language-genres-prompt.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{{country}}|{{language}}|{{genres}}|{{timeframe}}|{{maxAlbums}}");
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(templates, notifier, maxAlbumsPerQuery: 20);
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, CancellationToken.None);

        notifier.Verify(
            n => n.NotifyPromptRenderedAsync(
                "--- PROMPT 2: DISCOVERY QUERY ---",
                "Romania|Romanian|TBD|between 14-JUL-2026 and 13-AUG-2026|20",
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

        await step.ProcessAsync(bucket, CancellationToken.None);

        notifier.Verify(
            n => n.NotifyPromptRenderedAsync("--- PROMPT 2: DISCOVERY QUERY ---", "42", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
