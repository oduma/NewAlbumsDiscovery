using MediatR;
using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Domain.MusicAggregator;
using NewAlbumsDiscovery.Domain.MusicAggregator.Filtering;

namespace NewAlbumsDiscovery.Application.MusicAggregator;

/// <summary>
/// Triggers one full aggregation run: read all LovedTracks, normalize country synonyms, run the
/// cascading-threshold engine, drop continent-fallback/invalid-country buckets, atomically replace
/// AggregatedBuckets. No parameters — every run recalculates everything from scratch.
/// </summary>
public sealed record AggregateMusicPreferencesCommand : IRequest;

public sealed class AggregateMusicPreferencesCommandHandler : IRequestHandler<AggregateMusicPreferencesCommand>
{
    private readonly ILovedTrackRepository _lovedTrackRepository;
    private readonly IAggregatedBucketRepository _aggregatedBucketRepository;
    private readonly BucketAggregatorEngine _engine;
    private readonly IOptions<AggregatorSettings> _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ICountryMasterDataProvider _countryMasterDataProvider;
    private readonly CountryNormalizer _normalizer;
    private readonly IEnumerable<IBucketFilterRule> _filterRules;

    public AggregateMusicPreferencesCommandHandler(
        ILovedTrackRepository lovedTrackRepository,
        IAggregatedBucketRepository aggregatedBucketRepository,
        BucketAggregatorEngine engine,
        IOptions<AggregatorSettings> settings,
        TimeProvider timeProvider,
        ICountryMasterDataProvider countryMasterDataProvider,
        CountryNormalizer normalizer,
        IEnumerable<IBucketFilterRule> filterRules)
    {
        _lovedTrackRepository = lovedTrackRepository;
        _aggregatedBucketRepository = aggregatedBucketRepository;
        _engine = engine;
        _settings = settings;
        _timeProvider = timeProvider;
        _countryMasterDataProvider = countryMasterDataProvider;
        _normalizer = normalizer;
        _filterRules = filterRules;
    }

    public async Task Handle(AggregateMusicPreferencesCommand request, CancellationToken cancellationToken)
    {
        var tracks = await _lovedTrackRepository.GetAllAsync(cancellationToken);

        var synonyms = await _countryMasterDataProvider.GetCountrySynonymsAsync(cancellationToken);
        var normalizedTracks = _normalizer.Normalize(tracks, synonyms);

        var thresholds = new AggregationThresholds(
            _settings.Value.CountryRegionThreshold,
            _settings.Value.CountryRegionLanguageThreshold,
            _settings.Value.MinimumBucketThreshold);

        var asOfUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var buckets = _engine.Aggregate(normalizedTracks, thresholds, asOfUtc);

        var masterData = await _countryMasterDataProvider.GetCountryMasterDataAsync(cancellationToken);
        foreach (var rule in _filterRules)
        {
            buckets = rule.Apply(buckets, masterData);
        }

        await _aggregatedBucketRepository.ReplaceAllAsync(buckets, cancellationToken);
    }
}
