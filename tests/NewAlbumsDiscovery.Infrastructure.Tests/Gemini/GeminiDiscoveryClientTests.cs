using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NewAlbumsDiscovery.Application.AIDiscovery;
using NewAlbumsDiscovery.Infrastructure.Gemini;

namespace NewAlbumsDiscovery.Infrastructure.Tests.Gemini;

public class GeminiDiscoveryClientTests
{
    private const string ValidJson = "[{\"artist\":\"Artist A\",\"album\":\"Album A\"}]";

    /// <summary>
    /// Fires TimeProvider-based Task.Delay calls immediately (no real waiting) and records the
    /// requested delay for each call, so backoff behavior is verifiable without slowing tests down.
    /// </summary>
    private sealed class RecordingTimeProvider : TimeProvider
    {
        public List<TimeSpan> Delays { get; } = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays.Add(dueTime);
            callback(state);
            return new NoopTimer();
        }

        private sealed class NoopTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private static GeminiDiscoveryClient CreateClient(
        Mock<IGeminiApiClient> apiClient,
        RecordingTimeProvider timeProvider,
        GeminiOptions? options = null,
        ILogger<GeminiDiscoveryClient>? logger = null)
        => new(
            apiClient.Object,
            Options.Create(options ?? new GeminiOptions { MaxRetryAttempts = 3, InitialBackoffSeconds = 2 }),
            timeProvider,
            logger ?? NullLogger<GeminiDiscoveryClient>.Instance);

    private static DiscoveryPromptRequest Request(string country = "Romania", string? language = null, string? genre = null)
        => new(country, language, genre, MaxAlbums: 20, Timeframe: "last 30 days");

    [Fact]
    public async Task DiscoverAsync_WithValidJsonOnFirstAttempt_ReturnsMappedCandidates()
    {
        var apiClient = new Mock<IGeminiApiClient>();
        apiClient.Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidJson);
        var client = CreateClient(apiClient, new RecordingTimeProvider());

        var result = await client.DiscoverAsync(Request(), CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.Equal("Artist A", candidate.Artist);
        Assert.Equal("Album A", candidate.Album);
        apiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DiscoverAsync_WithMalformedJsonThenValidJson_RetriesAndSucceeds()
    {
        var apiClient = new Mock<IGeminiApiClient>();
        apiClient.SetupSequence(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not json")
            .ReturnsAsync(ValidJson);
        var timeProvider = new RecordingTimeProvider();
        var client = CreateClient(apiClient, timeProvider);

        var result = await client.DiscoverAsync(Request(), CancellationToken.None);

        Assert.Single(result);
        apiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Single(timeProvider.Delays);
        Assert.Equal(TimeSpan.FromSeconds(2), timeProvider.Delays[0]);
    }

    [Fact]
    public async Task DiscoverAsync_WithMalformedJsonEveryAttempt_ReturnsEmptyListAndLogsError()
    {
        var apiClient = new Mock<IGeminiApiClient>();
        apiClient.Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("not json");
        var timeProvider = new RecordingTimeProvider();
        var logger = new Mock<ILogger<GeminiDiscoveryClient>>();
        var client = CreateClient(apiClient, timeProvider, new GeminiOptions { MaxRetryAttempts = 3, InitialBackoffSeconds = 2 }, logger.Object);

        var result = await client.DiscoverAsync(Request(), CancellationToken.None);

        Assert.Empty(result);
        apiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DiscoverAsync_WithRateLimitExceptionThenSuccess_RetriesAndSucceeds()
    {
        var apiClient = new Mock<IGeminiApiClient>();
        apiClient.SetupSequence(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Rate limited", null, HttpStatusCode.TooManyRequests))
            .ReturnsAsync(ValidJson);
        var timeProvider = new RecordingTimeProvider();
        var client = CreateClient(apiClient, timeProvider);

        var result = await client.DiscoverAsync(Request(), CancellationToken.None);

        Assert.Single(result);
        apiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DiscoverAsync_BackoffDelaysDoubleEachAttempt()
    {
        var apiClient = new Mock<IGeminiApiClient>();
        apiClient.Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("not json");
        var timeProvider = new RecordingTimeProvider();
        var client = CreateClient(apiClient, timeProvider, new GeminiOptions { MaxRetryAttempts = 3, InitialBackoffSeconds = 2 });

        await client.DiscoverAsync(Request(), CancellationToken.None);

        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], timeProvider.Delays);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("Romanian", null)]
    [InlineData("Romanian", "Rock")]
    [InlineData(null, "Rock")]
    public void RenderPrompt_SelectsExpectedTemplateAndSubstitutesAllPlaceholders(string? language, string? genre)
    {
        var request = new DiscoveryPromptRequest("Romania", language, genre, MaxAlbums: 15, Timeframe: "last 30 days");

        var rendered = GeminiDiscoveryClient.RenderPrompt(request);

        Assert.Contains("Romania", rendered);
        Assert.Contains("15", rendered);
        Assert.Contains("last 30 days", rendered);
        Assert.DoesNotContain("{{country}}", rendered);
        Assert.DoesNotContain("{{timeframe}}", rendered);
        Assert.DoesNotContain("{{maxAlbums}}", rendered);

        if (genre is not null)
        {
            Assert.Contains(genre, rendered);
            Assert.DoesNotContain("{{genres}}", rendered);
        }

        if (language is not null)
        {
            Assert.Contains(language, rendered);
            Assert.DoesNotContain("{{language}}", rendered);
        }
    }

    [Fact]
    public async Task DiscoverAsync_PassesRenderedPromptToApiClient()
    {
        var apiClient = new Mock<IGeminiApiClient>();
        apiClient.Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidJson);
        var client = CreateClient(apiClient, new RecordingTimeProvider());

        await client.DiscoverAsync(Request(country: "Malta", language: "Maltese"), CancellationToken.None);

        apiClient.Verify(c => c.GenerateContentAsync(
            It.Is<string>(p => p.Contains("Malta") && p.Contains("Maltese")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
