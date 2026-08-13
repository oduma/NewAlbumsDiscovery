using MediatR;
using Microsoft.Extensions.Options;
using Moq;
using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;
using NewAlbumsDiscovery.Application.CoreOperations;
using NewAlbumsDiscovery.Application.MusicAggregator;
using NewAlbumsDiscovery.Application.Tests.TestSupport;

namespace NewAlbumsDiscovery.Application.Tests.CoreOperations;

public class RunOrchestrationCommandHandlerTests
{
    private static RunOrchestrationCommandHandler CreateHandler(
        Mock<ISender> sender,
        RecordingTimeProvider timeProvider,
        int triggerDelaySeconds = 30)
        => new(sender.Object, Options.Create(new CoreOperationsOptions { TriggerDelaySeconds = triggerDelaySeconds }), timeProvider);

    [Fact]
    public async Task Handle_SendsAggregationThenDelaysThenSendsPipeline_InOrder()
    {
        var callOrder = new List<string>();
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<AggregateMusicPreferencesCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("aggregate"))
            .Returns(Unit.Task);
        sender.Setup(s => s.Send(It.IsAny<RunAIDiscoveryPipelineCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("pipeline"))
            .Returns(Unit.Task);
        var timeProvider = new RecordingTimeProvider();
        var handler = CreateHandler(sender, timeProvider, triggerDelaySeconds: 30);

        await handler.Handle(new RunOrchestrationCommand(), CancellationToken.None);

        Assert.Equal(["aggregate", "pipeline"], callOrder);
        Assert.Single(timeProvider.Delays);
        Assert.Equal(TimeSpan.FromSeconds(30), timeProvider.Delays[0]);
        sender.Verify(s => s.Send(It.IsAny<AggregateMusicPreferencesCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        sender.Verify(s => s.Send(It.IsAny<RunAIDiscoveryPipelineCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AggregationThrows_DelayAndPipelineNeverRun()
    {
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<AggregateMusicPreferencesCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("aggregation failed"));
        var timeProvider = new RecordingTimeProvider();
        var handler = CreateHandler(sender, timeProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new RunOrchestrationCommand(), CancellationToken.None));

        Assert.Empty(timeProvider.Delays);
        sender.Verify(s => s.Send(It.IsAny<RunAIDiscoveryPipelineCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
