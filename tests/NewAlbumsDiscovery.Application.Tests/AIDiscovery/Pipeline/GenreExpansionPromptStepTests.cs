using Microsoft.Extensions.Options;
using Moq;
using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;
using NewAlbumsDiscovery.Application.AIDiscovery.Prompts;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.Tests.AIDiscovery.Pipeline;

public class GenreExpansionPromptStepTests
{
    private static readonly DateTime AsOfUtc = new(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);

    private static GenreExpansionPromptStep CreateStep(
        Mock<IPromptTemplateProvider> templates,
        Mock<IDiscoveryNotifier> notifier,
        string instrumentalLanguage = "Instrumental")
    {
        var options = Options.Create(new AIDiscoveryOptions { InstrumentalLanguage = instrumentalLanguage });
        return new GenreExpansionPromptStep(templates.Object, new PromptRenderer(), notifier.Object, options);
    }

    [Fact]
    public async Task ProcessAsync_WithNoGenre_NeverRequestsTemplateOrNotifies()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(templates, notifier);
        var bucket = AggregatedBucket.Create("Romania/Romanian", BucketType.CountryLanguage, "Romania", "Romanian", null, 5, AsOfUtc);

        await step.ProcessAsync(bucket, CancellationToken.None);

        templates.Verify(t => t.GetTemplateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        notifier.Verify(n => n.NotifyPromptRenderedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithGenreButInstrumental_NeverRequestsTemplateOrNotifies()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(templates, notifier);
        var bucket = AggregatedBucket.Create("Romania/Instrumental/Ambient", BucketType.CountryLanguageGenre, "Romania", "Instrumental", "Ambient", 5, AsOfUtc);

        await step.ProcessAsync(bucket, CancellationToken.None);

        templates.Verify(t => t.GetTemplateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        notifier.Verify(n => n.NotifyPromptRenderedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithGenreAndNotInstrumental_RendersAndNotifies()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates
            .Setup(t => t.GetTemplateAsync("genre-expansion-prompt.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Genre: {{genre}} Country: {{country}} Language: {{language}}");
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(templates, notifier);
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, CancellationToken.None);

        notifier.Verify(
            n => n.NotifyPromptRenderedAsync(
                "--- PROMPT 1: GENRE EXPANSION ---",
                "Genre: Indie Pop Country: Romania Language: Romanian",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithGenreAndNullLanguage_SubstitutesEmptyStringForLanguage()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates
            .Setup(t => t.GetTemplateAsync("genre-expansion-prompt.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Language: [{{language}}]");
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(templates, notifier);
        var bucket = AggregatedBucket.Create("Romania/Indie Pop", BucketType.CountryLanguageGenre, "Romania", null, "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, CancellationToken.None);

        notifier.Verify(
            n => n.NotifyPromptRenderedAsync(
                "--- PROMPT 1: GENRE EXPANSION ---",
                "Language: []",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_PassesCancellationTokenThrough()
    {
        using var cts = new CancellationTokenSource();
        var templates = new Mock<IPromptTemplateProvider>();
        templates
            .Setup(t => t.GetTemplateAsync(It.IsAny<string>(), cts.Token))
            .ReturnsAsync("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var step = CreateStep(templates, notifier);
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, cts.Token);

        templates.Verify(t => t.GetTemplateAsync(It.IsAny<string>(), cts.Token), Times.Once);
        notifier.Verify(n => n.NotifyPromptRenderedAsync(It.IsAny<string>(), It.IsAny<string>(), cts.Token), Times.Once);
    }
}
