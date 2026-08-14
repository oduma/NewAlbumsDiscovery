using Microsoft.Extensions.Options;
using Moq;
using NewAlbumsDiscovery.Application.AIDiscovery;
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
        Mock<IGeminiClient> geminiClient,
        RecordingTimeProvider? timeProvider = null,
        int maxAlbumsPerQuery = 20,
        string instrumentalLanguage = "Instrumental",
        int[]? retryBackoffSeconds = null)
    {
        var options = Options.Create(new AIDiscoveryOptions
        {
            MaxAlbumsPerQuery = maxAlbumsPerQuery,
            InstrumentalLanguage = instrumentalLanguage,
        });
        var geminiOptions = Options.Create(new GeminiOptions { RetryBackoffSeconds = retryBackoffSeconds ?? [10, 30, 180] });
        var effectiveTimeProvider = timeProvider ?? new RecordingTimeProvider(FixedUtcNow);
        var retryExecutor = new GeminiRetryExecutor(geminiClient.Object, effectiveTimeProvider);
        return new DiscoveryQueryPromptStep(
            templates.Object,
            new PromptRenderer(),
            new TimeframeFormatter(),
            notifier.Object,
            effectiveTimeProvider,
            retryExecutor,
            options,
            geminiOptions);
    }

    private static Mock<IGeminiClient> GeminiClientReturning(string responseText)
    {
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Success(responseText));
        return geminiClient;
    }

    [Fact]
    public async Task ProcessAsync_WithInstrumentalCountryLanguageBucket_UsesCountryInstrumentalPromptAndOmitsGenres()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates
            .Setup(t => t.GetTemplateAsync("country-instrumental-prompt.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{{country}}|{{timeframe}}|{{maxAlbums}}|{{genres}}");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = GeminiClientReturning("[]");
        var step = CreateStep(templates, notifier, geminiClient, maxAlbumsPerQuery: 20);
        var bucket = AggregatedBucket.Create("Romania/Instrumental", BucketType.CountryLanguage, "Romania", "Instrumental", null, 5, AsOfUtc);

        await step.ProcessAsync(bucket, new BucketProcessingState(), new HashSet<AlbumKey>(), CancellationToken.None);

        geminiClient.Verify(
            c => c.GenerateContentAsync("Romania|between 14-JUL-2026 and 13-AUG-2026|20|{{genres}}", It.IsAny<CancellationToken>()),
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
        var geminiClient = GeminiClientReturning("[]");
        var step = CreateStep(templates, notifier, geminiClient, maxAlbumsPerQuery: 20);
        var bucket = AggregatedBucket.Create("Romania/Instrumental/Ambient", BucketType.CountryLanguageGenre, "Romania", "Instrumental", "Ambient", 5, AsOfUtc);

        await step.ProcessAsync(bucket, new BucketProcessingState(), new HashSet<AlbumKey>(), CancellationToken.None);

        geminiClient.Verify(
            c => c.GenerateContentAsync("Romania|Ambient|between 14-JUL-2026 and 13-AUG-2026|20", It.IsAny<CancellationToken>()),
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
        var geminiClient = GeminiClientReturning("[]");
        var step = CreateStep(templates, notifier, geminiClient, instrumentalLanguage: "NoVocals");
        var bucket = AggregatedBucket.Create("Romania/NoVocals", BucketType.CountryLanguage, "Romania", "NoVocals", null, 5, AsOfUtc);

        await step.ProcessAsync(bucket, new BucketProcessingState(), new HashSet<AlbumKey>(), CancellationToken.None);

        geminiClient.Verify(c => c.GenerateContentAsync("Romania", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithCountryBucket_UsesCountryPromptAndOmitsLanguageAndGenres()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates
            .Setup(t => t.GetTemplateAsync("country-prompt.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{{country}}|{{timeframe}}|{{maxAlbums}}|{{language}}|{{genres}}");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = GeminiClientReturning("[]");
        var step = CreateStep(templates, notifier, geminiClient, maxAlbumsPerQuery: 20);
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 15, AsOfUtc);

        await step.ProcessAsync(bucket, new BucketProcessingState(), new HashSet<AlbumKey>(), CancellationToken.None);

        geminiClient.Verify(
            c => c.GenerateContentAsync("Romania|between 14-JUL-2026 and 13-AUG-2026|20|{{language}}|{{genres}}", It.IsAny<CancellationToken>()),
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
        var geminiClient = GeminiClientReturning("[]");
        var step = CreateStep(templates, notifier, geminiClient, maxAlbumsPerQuery: 20);
        var bucket = AggregatedBucket.Create("Romania/Romanian", BucketType.CountryLanguage, "Romania", "Romanian", null, 15, AsOfUtc);

        await step.ProcessAsync(bucket, new BucketProcessingState(), new HashSet<AlbumKey>(), CancellationToken.None);

        geminiClient.Verify(
            c => c.GenerateContentAsync("Romania|Romanian|between 14-JUL-2026 and 13-AUG-2026|20|{{genres}}", It.IsAny<CancellationToken>()),
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
        var geminiClient = GeminiClientReturning("[]");
        var step = CreateStep(templates, notifier, geminiClient, maxAlbumsPerQuery: 20);
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);
        var state = new BucketProcessingState { ResolvedGenres = "Alt Pop, Bedroom Pop" };

        await step.ProcessAsync(bucket, state, new HashSet<AlbumKey>(), CancellationToken.None);

        geminiClient.Verify(
            c => c.GenerateContentAsync("Romania|Romanian|Alt Pop, Bedroom Pop|between 14-JUL-2026 and 13-AUG-2026|20", It.IsAny<CancellationToken>()),
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
        var geminiClient = GeminiClientReturning("[]");
        var step = CreateStep(templates, notifier, geminiClient, maxAlbumsPerQuery: 42);
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 15, AsOfUtc);

        await step.ProcessAsync(bucket, new BucketProcessingState(), new HashSet<AlbumKey>(), CancellationToken.None);

        geminiClient.Verify(c => c.GenerateContentAsync("42", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithWellFormedAlbumArrayResponse_PopulatesDiscoveredCandidatesInOrder()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates.Setup(t => t.GetTemplateAsync("country-prompt.md", It.IsAny<CancellationToken>())).ReturnsAsync("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = GeminiClientReturning("[{\"artist\":\"Daft Punk\",\"album\":\"Discovery\"},{\"artist\":\"Justice\",\"album\":\"Cross\"}]");
        var step = CreateStep(templates, notifier, geminiClient);
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 15, AsOfUtc);
        var state = new BucketProcessingState();

        await step.ProcessAsync(bucket, state, new HashSet<AlbumKey>(), CancellationToken.None);

        Assert.Equal([("Daft Punk", "Discovery"), ("Justice", "Cross")], state.DiscoveredCandidates);
        Assert.False(state.IsAbandoned);
    }

    [Fact]
    public async Task ProcessAsync_WithMalformedJson_YieldsNoCandidatesWithoutRetryingOrAbandoning()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates.Setup(t => t.GetTemplateAsync("country-prompt.md", It.IsAny<CancellationToken>())).ReturnsAsync("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = GeminiClientReturning("not json");
        var timeProvider = new RecordingTimeProvider(FixedUtcNow);
        var step = CreateStep(templates, notifier, geminiClient, timeProvider);
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 15, AsOfUtc);
        var state = new BucketProcessingState();

        await step.ProcessAsync(bucket, state, new HashSet<AlbumKey>(), CancellationToken.None);

        Assert.Empty(state.DiscoveredCandidates);
        Assert.False(state.IsAbandoned);
        Assert.Empty(timeProvider.Delays);
        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(n => n.NotifyBucketAbandonedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithTransientFailureThenSuccess_RetriesThenPopulatesCandidates()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates.Setup(t => t.GetTemplateAsync("country-prompt.md", It.IsAny<CancellationToken>())).ReturnsAsync("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .SetupSequence(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Transient("network error"))
            .ReturnsAsync(GeminiCallResult.Success("[{\"artist\":\"Daft Punk\",\"album\":\"Discovery\"}]"));
        var timeProvider = new RecordingTimeProvider(FixedUtcNow);
        var step = CreateStep(templates, notifier, geminiClient, timeProvider);
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 15, AsOfUtc);
        var state = new BucketProcessingState();

        await step.ProcessAsync(bucket, state, new HashSet<AlbumKey>(), CancellationToken.None);

        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Equal([TimeSpan.FromSeconds(10)], timeProvider.Delays);
        Assert.Equal([("Daft Punk", "Discovery")], state.DiscoveredCandidates);
        Assert.False(state.IsAbandoned);
    }

    [Fact]
    public async Task ProcessAsync_WithAllRetriesExhausted_AbandonsAfterConfiguredBackoffs()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates.Setup(t => t.GetTemplateAsync("country-prompt.md", It.IsAny<CancellationToken>())).ReturnsAsync("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Transient("rate limited"));
        var timeProvider = new RecordingTimeProvider(FixedUtcNow);
        var step = CreateStep(templates, notifier, geminiClient, timeProvider, retryBackoffSeconds: [10, 30, 180]);
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 15, AsOfUtc);
        var state = new BucketProcessingState();

        await step.ProcessAsync(bucket, state, new HashSet<AlbumKey>(), CancellationToken.None);

        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
        Assert.Equal([TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(180)], timeProvider.Delays);
        Assert.True(state.IsAbandoned);
        Assert.Empty(state.DiscoveredCandidates);
        notifier.Verify(
            n => n.NotifyBucketAbandonedAsync(bucket.BucketName, bucket.TrackCount, "rate limited", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithJsonNullResponse_YieldsNoCandidatesWithoutRetryingOrAbandoning()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates.Setup(t => t.GetTemplateAsync("country-prompt.md", It.IsAny<CancellationToken>())).ReturnsAsync("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = GeminiClientReturning("null");
        var timeProvider = new RecordingTimeProvider(FixedUtcNow);
        var step = CreateStep(templates, notifier, geminiClient, timeProvider);
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 15, AsOfUtc);
        var state = new BucketProcessingState();

        await step.ProcessAsync(bucket, state, new HashSet<AlbumKey>(), CancellationToken.None);

        Assert.Empty(state.DiscoveredCandidates);
        Assert.False(state.IsAbandoned);
        Assert.Empty(timeProvider.Delays);
    }

    [Fact]
    public async Task ProcessAsync_WithFailureAndNullErrorMessage_UsesFallbackAbandonmentReason()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates.Setup(t => t.GetTemplateAsync("country-prompt.md", It.IsAny<CancellationToken>())).ReturnsAsync("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiCallResult(IsSuccess: false, IsTransientFailure: false, ResponseText: null, ErrorMessage: null));
        var step = CreateStep(templates, notifier, geminiClient);
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 15, AsOfUtc);
        var state = new BucketProcessingState();

        await step.ProcessAsync(bucket, state, new HashSet<AlbumKey>(), CancellationToken.None);

        Assert.True(state.IsAbandoned);
        notifier.Verify(
            n => n.NotifyBucketAbandonedAsync(bucket.BucketName, bucket.TrackCount, "Gemini API call failed.", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithNonTransientFailure_AbandonsImmediatelyWithoutRetrying()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates.Setup(t => t.GetTemplateAsync("country-prompt.md", It.IsAny<CancellationToken>())).ReturnsAsync("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Permanent("invalid api key"));
        var timeProvider = new RecordingTimeProvider(FixedUtcNow);
        var step = CreateStep(templates, notifier, geminiClient, timeProvider);
        var bucket = AggregatedBucket.Create("Romania", BucketType.Country, "Romania", null, null, 15, AsOfUtc);
        var state = new BucketProcessingState();

        await step.ProcessAsync(bucket, state, new HashSet<AlbumKey>(), CancellationToken.None);

        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(timeProvider.Delays);
        Assert.True(state.IsAbandoned);
        notifier.Verify(
            n => n.NotifyBucketAbandonedAsync(bucket.BucketName, bucket.TrackCount, "invalid api key", It.IsAny<CancellationToken>()),
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
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), cts.Token))
            .ReturnsAsync(GeminiCallResult.Success("[]"));
        var step = CreateStep(templates, notifier, geminiClient);
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);
        var state = new BucketProcessingState { ResolvedGenres = "Indie Pop" };

        await step.ProcessAsync(bucket, state, new HashSet<AlbumKey>(), cts.Token);

        templates.Verify(t => t.GetTemplateAsync(It.IsAny<string>(), cts.Token), Times.Once);
        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), cts.Token), Times.Once);
    }
}
