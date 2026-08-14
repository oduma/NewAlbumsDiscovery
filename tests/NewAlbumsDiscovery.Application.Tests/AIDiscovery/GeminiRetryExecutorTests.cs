using Moq;
using NewAlbumsDiscovery.Application.AIDiscovery;
using NewAlbumsDiscovery.Application.Tests.TestSupport;

namespace NewAlbumsDiscovery.Application.Tests.AIDiscovery;

public class GeminiRetryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WithSuccessOnFirstAttempt_ReturnsSuccessWithoutDelay()
    {
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Success("response"));
        var timeProvider = new RecordingTimeProvider();
        var executor = new GeminiRetryExecutor(geminiClient.Object, timeProvider);

        var result = await executor.ExecuteAsync("prompt", [10, 30, 180], CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("response", result.ResponseText);
        Assert.Empty(timeProvider.Delays);
        geminiClient.Verify(c => c.GenerateContentAsync("prompt", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithTransientFailureThenSuccess_DelaysOnceThenReturnsSuccess()
    {
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .SetupSequence(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Transient("network error"))
            .ReturnsAsync(GeminiCallResult.Success("response"));
        var timeProvider = new RecordingTimeProvider();
        var executor = new GeminiRetryExecutor(geminiClient.Object, timeProvider);

        var result = await executor.ExecuteAsync("prompt", [10, 30, 180], CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([TimeSpan.FromSeconds(10)], timeProvider.Delays);
        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_WithAllRetriesExhausted_DelaysForEveryBackoffThenReturnsFinalTransientResult()
    {
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Transient("rate limited"));
        var timeProvider = new RecordingTimeProvider();
        var executor = new GeminiRetryExecutor(geminiClient.Object, timeProvider);

        var result = await executor.ExecuteAsync("prompt", [10, 30, 180], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTransientFailure);
        Assert.Equal("rate limited", result.ErrorMessage);
        Assert.Equal([TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(180)], timeProvider.Delays);
        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [Fact]
    public async Task ExecuteAsync_WithPermanentFailureOnFirstAttempt_ReturnsImmediatelyWithoutDelay()
    {
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeminiCallResult.Permanent("invalid api key"));
        var timeProvider = new RecordingTimeProvider();
        var executor = new GeminiRetryExecutor(geminiClient.Object, timeProvider);

        var result = await executor.ExecuteAsync("prompt", [10, 30, 180], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsTransientFailure);
        Assert.Equal("invalid api key", result.ErrorMessage);
        Assert.Empty(timeProvider.Delays);
        geminiClient.Verify(c => c.GenerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenThrough()
    {
        using var cts = new CancellationTokenSource();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(c => c.GenerateContentAsync(It.IsAny<string>(), cts.Token))
            .ReturnsAsync(GeminiCallResult.Success("response"));
        var executor = new GeminiRetryExecutor(geminiClient.Object, new RecordingTimeProvider());

        await executor.ExecuteAsync("prompt", [10], cts.Token);

        geminiClient.Verify(c => c.GenerateContentAsync("prompt", cts.Token), Times.Once);
    }
}
