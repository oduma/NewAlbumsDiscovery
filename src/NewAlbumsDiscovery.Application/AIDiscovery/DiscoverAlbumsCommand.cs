using MediatR;
using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Application.MusicAggregator;
using NewAlbumsDiscovery.Domain.AIDiscovery;

namespace NewAlbumsDiscovery.Application.AIDiscovery;

/// <summary>
/// Runs one full discovery pass: for every current AggregatedBucket, ask Gemini for candidate
/// albums, filter out anything already known (globally, across all buckets — see
/// docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 4 decision #4), and persist the rest.
/// No parameters — reads whatever AggregatedBuckets currently exist at invocation time.
/// Deliberately NOT wired into any automatic trigger in this phase (deferred to a future phase).
/// </summary>
public sealed record DiscoverAlbumsCommand : IRequest;

public sealed class DiscoverAlbumsCommandHandler : IRequestHandler<DiscoverAlbumsCommand>
{
    private readonly IAggregatedBucketRepository _aggregatedBucketRepository;
    private readonly IDiscoveredAlbumRepository _discoveredAlbumRepository;
    private readonly IGeminiDiscoveryClient _geminiDiscoveryClient;
    private readonly IOptions<GeminiOptions> _options;
    private readonly TimeProvider _timeProvider;

    public DiscoverAlbumsCommandHandler(
        IAggregatedBucketRepository aggregatedBucketRepository,
        IDiscoveredAlbumRepository discoveredAlbumRepository,
        IGeminiDiscoveryClient geminiDiscoveryClient,
        IOptions<GeminiOptions> options,
        TimeProvider timeProvider)
    {
        _aggregatedBucketRepository = aggregatedBucketRepository;
        _discoveredAlbumRepository = discoveredAlbumRepository;
        _geminiDiscoveryClient = geminiDiscoveryClient;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task Handle(DiscoverAlbumsCommand request, CancellationToken cancellationToken)
    {
        var buckets = await _aggregatedBucketRepository.GetAllAsync(cancellationToken);
        var existingAlbums = await _discoveredAlbumRepository.GetAllAsync(cancellationToken);

        var existingKeys = new HashSet<AlbumKey>(
            existingAlbums.Select(album => AlbumKey.From(album.Artist, album.Album)));

        var discoveredAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var newAlbums = new List<DiscoveredAlbum>();

        foreach (var bucket in buckets)
        {
            var promptRequest = new DiscoveryPromptRequest(
                bucket.Country,
                bucket.Language,
                bucket.Genre,
                _options.Value.MaxAlbumsPerPrompt,
                _options.Value.Timeframe);

            var candidates = await _geminiDiscoveryClient.DiscoverAsync(promptRequest, cancellationToken);
            var candidateTuples = candidates.Select(c => (c.Artist, c.Album)).ToList();
            var freshCandidates = AlbumDeduplicator.SelectNew(candidateTuples, existingKeys);

            foreach (var (artist, album) in freshCandidates)
            {
                newAlbums.Add(DiscoveredAlbum.Create(
                    artist, album, bucket.Country, bucket.Language, bucket.Genre, discoveredAtUtc));
            }
        }

        if (newAlbums.Count > 0)
        {
            await _discoveredAlbumRepository.AddRangeAsync(newAlbums, cancellationToken);
        }
    }
}
