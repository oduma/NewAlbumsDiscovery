using MediatR;
using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.MusicAggregator;

/// <summary>
/// Triggers one full aggregation run: read all LovedTracks, run the cascading-threshold engine,
/// atomically replace AggregatedBuckets. No parameters — every run recalculates everything from scratch.
/// </summary>
public sealed record AggregateMusicPreferencesCommand : IRequest;

public sealed class AggregateMusicPreferencesCommandHandler : IRequestHandler<AggregateMusicPreferencesCommand>
{
    private readonly ILovedTrackRepository _lovedTrackRepository;
    private readonly IAggregatedBucketRepository _aggregatedBucketRepository;
    private readonly BucketAggregatorEngine _engine;
    private readonly IOptions<AggregatorSettings> _settings;
    private readonly TimeProvider _timeProvider;

    public AggregateMusicPreferencesCommandHandler(
        ILovedTrackRepository lovedTrackRepository,
        IAggregatedBucketRepository aggregatedBucketRepository,
        BucketAggregatorEngine engine,
        IOptions<AggregatorSettings> settings,
        TimeProvider timeProvider)
    {
        _lovedTrackRepository = lovedTrackRepository;
        _aggregatedBucketRepository = aggregatedBucketRepository;
        _engine = engine;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    public async Task Handle(AggregateMusicPreferencesCommand request, CancellationToken cancellationToken)
    {
        var tracks = await _lovedTrackRepository.GetAllAsync(cancellationToken);

        var thresholds = new AggregationThresholds(
            _settings.Value.CountryRegionThreshold,
            _settings.Value.CountryRegionLanguageThreshold,
            _settings.Value.MinimumBucketThreshold);

        var asOfUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var buckets = _engine.Aggregate(tracks, thresholds, asOfUtc);

        await _aggregatedBucketRepository.ReplaceAllAsync(buckets, cancellationToken);
    }
}
