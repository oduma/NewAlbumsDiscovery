using Microsoft.Extensions.Options;
using Moq;
using NewAlbumsDiscovery.Application.AIDiscovery;
using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;
using NewAlbumsDiscovery.Application.AIDiscovery.Prompts;
using NewAlbumsDiscovery.Application.Tests.TestSupport;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.Tests.AIDiscovery.Pipeline;

public class GenreExpansionPromptStepTests
{
    private static readonly DateTime AsOfUtc = new(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);

    private static GenreExpansionPromptStep CreateStep(
        Mock<IPromptTemplateProvider> templates,
        Mock<IDiscoveryNotifier> notifier,
        Mock<IGeminiClient> geminiClient,
        RecordingTimeProvider timeProvider,
        string instrumentalLanguage = "Instrumental",
        int[]? retryBackoffSeconds = null)
    {
        var aiDiscoveryOptions = Options.Create(new AIDiscoveryOptions { InstrumentalLanguage = instrumentalLanguage });
        var geminiOptions = Options.Create(new GeminiOptions { RetryBackoffSeconds = retryBackoffSeconds ?? [10, 30, 180] });
        return new GenreExpansionPromptStep(
            templates.Object, new PromptRenderer(), notifier.Object, geminiClient.Object, aiDiscoveryOptions, geminiOptions, timeProvider);
    }

    private static Mock<IPromptTemplateProvider> TemplateReturning(string content)
    {
        var templates = new Mock<IPromptTemplateProvider>();
        templates
            .Setup(t => t.GetTemplateAsync("genre-expansion-prompt.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        return templates;
    }

    [Fact]
    public async Task ProcessAsync_WithNoGenre_NeverCallsGeminiClient()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        var state = new BucketProcessingState();
        var step = CreateStep(templates, notifier, geminiClient, new RecordingTimeProvider());
        var bucket = AggregatedBucket.Create("Romania/Romanian", BucketType.CountryLanguage, "Romania", "Romanian", null, 5, AsOfUtc);

        await step.ProcessAsync(bucket, state, CancellationToken.None);

        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(state.IsAbandoned);
        Assert.Null(state.ResolvedGenres);
    }

    [Fact]
    public async Task ProcessAsync_WithGenreButInstrumental_NeverCallsGeminiClient()
    {
        var templates = new Mock<IPromptTemplateProvider>();
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        var state = new BucketProcessingState();
        var step = CreateStep(templates, notifier, geminiClient, new RecordingTimeProvider());
        var bucket = AggregatedBucket.Create("Romania/Instrumental/Ambient", BucketType.CountryLanguageGenre, "Romania", "Instrumental", "Ambient", 5, AsOfUtc);

        await step.ProcessAsync(bucket, state, CancellationToken.None);

        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(state.IsAbandoned);
    }

    [Fact]
    public async Task ProcessAsync_WithSuccessfulCall_NeverPrintsPromptToConsole()
    {
        var templates = TemplateReturning("Genre: {{genre}} Country: {{country}} Language: {{language}}");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Success("[\"Indie Pop\"]"));
        var state = new BucketProcessingState();
        var step = CreateStep(templates, notifier, geminiClient, new RecordingTimeProvider());
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, state, CancellationToken.None);

        notifier.Verify(n => n.NotifyPromptRenderedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithGenreAndNullLanguage_SubstitutesEmptyStringForLanguage()
    {
        var templates = TemplateReturning("Language: [{{language}}]");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync("Language: []", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Success("[\"Indie Pop\"]"));
        var state = new BucketProcessingState();
        var step = CreateStep(templates, notifier, geminiClient, new RecordingTimeProvider());
        var bucket = AggregatedBucket.Create("Romania/Indie Pop", BucketType.CountryLanguageGenre, "Romania", null, "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, state, CancellationToken.None);

        geminiClient.Verify(c => c.GenerateContentAsync("Language: []", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithSuccessfulCall_SendsRenderedPromptToGeminiClient()
    {
        var templates = TemplateReturning("Genre: {{genre}} Country: {{country}} Language: {{language}}");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Success("[\"Indie Pop\"]"));
        var state = new BucketProcessingState();
        var step = CreateStep(templates, notifier, geminiClient, new RecordingTimeProvider());
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, state, CancellationToken.None);

        geminiClient.Verify(
            c => c.GenerateContentAsync("Genre: Indie Pop Country: Romania Language: Romanian", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithSuccessfulCall_JoinsJsonArrayIntoResolvedGenres()
    {
        var templates = TemplateReturning("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Success("[\"Genre A\",\"Genre B\"]"));
        var state = new BucketProcessingState();
        var step = CreateStep(templates, notifier, geminiClient, new RecordingTimeProvider());
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, state, CancellationToken.None);

        Assert.Equal("Genre A, Genre B", state.ResolvedGenres);
        Assert.False(state.IsAbandoned);
    }

    [Fact]
    public async Task ProcessAsync_WithEmptyJsonArray_FallsBackToOriginalGenre()
    {
        var templates = TemplateReturning("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Success("[]"));
        var state = new BucketProcessingState();
        var step = CreateStep(templates, notifier, geminiClient, new RecordingTimeProvider());
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, state, CancellationToken.None);

        Assert.Equal("Indie Pop", state.ResolvedGenres);
        Assert.False(state.IsAbandoned);
    }

    [Fact]
    public async Task ProcessAsync_WithUnparsableJson_FallsBackToOriginalGenreWithoutRetrying()
    {
        var templates = TemplateReturning("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Success("not json"));
        var state = new BucketProcessingState();
        var step = CreateStep(templates, notifier, geminiClient, new RecordingTimeProvider());
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, state, CancellationToken.None);

        Assert.Equal("Indie Pop", state.ResolvedGenres);
        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithTransientFailureThenSuccess_RetriesOnceAndSucceeds()
    {
        var templates = TemplateReturning("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .SetupSequence(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Transient("network error"))
            .ReturnsAsync(GeminiCallResult.Success("[\"Indie Pop\"]"));
        var state = new BucketProcessingState();
        var timeProvider = new RecordingTimeProvider();
        var step = CreateStep(templates, notifier, geminiClient, timeProvider);
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, state, CancellationToken.None);

        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Equal([TimeSpan.FromSeconds(10)], timeProvider.Delays);
        Assert.Equal("Indie Pop", state.ResolvedGenres);
        Assert.False(state.IsAbandoned);
    }

    [Fact]
    public async Task ProcessAsync_WithAllRetriesExhausted_AbandonsAfterConfiguredBackoffs()
    {
        var templates = TemplateReturning("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Transient("rate limited"));
        var state = new BucketProcessingState();
        var timeProvider = new RecordingTimeProvider();
        var step = CreateStep(templates, notifier, geminiClient, timeProvider, retryBackoffSeconds: [10, 30, 180]);
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, state, CancellationToken.None);

        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
        Assert.Equal(
            [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(180)],
            timeProvider.Delays);
        Assert.True(state.IsAbandoned);
        Assert.Null(state.ResolvedGenres);
        notifier.Verify(
            n => n.NotifyBucketAbandonedAsync(bucket.BucketName, bucket.TrackCount, "rate limited", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithNonTransientFailure_AbandonsImmediatelyWithoutRetrying()
    {
        var templates = TemplateReturning("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Permanent("invalid api key"));
        var state = new BucketProcessingState();
        var timeProvider = new RecordingTimeProvider();
        var step = CreateStep(templates, notifier, geminiClient, timeProvider);
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, state, CancellationToken.None);

        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(timeProvider.Delays);
        Assert.True(state.IsAbandoned);
        notifier.Verify(
            n => n.NotifyBucketAbandonedAsync(bucket.BucketName, bucket.TrackCount, "invalid api key", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithFailureAndNullErrorMessage_UsesFallbackAbandonmentReason()
    {
        var templates = TemplateReturning("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiCallResult(IsSuccess: false, IsTransientFailure: false, ResponseText: null, ErrorMessage: null));
        var state = new BucketProcessingState();
        var step = CreateStep(templates, notifier, geminiClient, new RecordingTimeProvider());
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, state, CancellationToken.None);

        Assert.True(state.IsAbandoned);
        notifier.Verify(
            n => n.NotifyBucketAbandonedAsync(bucket.BucketName, bucket.TrackCount, "Gemini API call failed.", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithNonDefaultRetryBackoffSeconds_UsesConfiguredValues()
    {
        var templates = TemplateReturning("template");
        var notifier = new Mock<IDiscoveryNotifier>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Transient("network error"));
        var state = new BucketProcessingState();
        var timeProvider = new RecordingTimeProvider();
        var step = CreateStep(templates, notifier, geminiClient, timeProvider, retryBackoffSeconds: [1, 2]);
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, state, CancellationToken.None);

        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], timeProvider.Delays);
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
            .ReturnsAsync(GeminiCallResult.Success("[\"Indie Pop\"]"));
        var state = new BucketProcessingState();
        var step = CreateStep(templates, notifier, geminiClient, new RecordingTimeProvider());
        var bucket = AggregatedBucket.Create("Romania/Romanian/Indie Pop", BucketType.CountryLanguageGenre, "Romania", "Romanian", "Indie Pop", 15, AsOfUtc);

        await step.ProcessAsync(bucket, state, cts.Token);

        templates.Verify(t => t.GetTemplateAsync(It.IsAny<string>(), cts.Token), Times.Once);
        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), cts.Token), Times.Once);
    }
}
