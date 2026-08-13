namespace NewAlbumsDiscovery.Application.Tests.TestSupport;

/// <summary>
/// Fires TimeProvider-based Task.Delay calls immediately (no real waiting) and records the
/// requested delay for each call, so pacing/backoff behavior is verifiable without slowing tests
/// down. Mirrors NewAlbumsDiscovery.Infrastructure.Tests.Gemini.GeminiDiscoveryClientTests'
/// private RecordingTimeProvider, shared here because Phase 5 needs it in multiple test files.
/// </summary>
public sealed class RecordingTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public List<TimeSpan> Delays { get; } = [];

    public RecordingTimeProvider(DateTimeOffset? utcNow = null)
    {
        _utcNow = utcNow ?? DateTimeOffset.UtcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

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
