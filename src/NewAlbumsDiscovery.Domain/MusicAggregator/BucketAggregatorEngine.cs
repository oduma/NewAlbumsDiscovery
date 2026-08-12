namespace NewAlbumsDiscovery.Domain.MusicAggregator;

/// <summary>
/// Pure, deterministic 3-level cascading-threshold aggregation (docs/requirements/phase3-requirements.md §4).
/// Zero dependencies, no I/O, no wall-clock reads — <paramref name="asOfUtc"/> in <see cref="Aggregate"/>
/// is always caller-supplied so results are fully reproducible in tests.
///
/// Level 2/3 grouping keys are composite (Country, Language[, Genre]), achieved implicitly by nesting
/// the language/genre grouping inside each country's own loop iteration — never grouping by language or
/// genre alone across different cascading countries.
/// </summary>
public sealed class BucketAggregatorEngine
{
    public IReadOnlyList<AggregatedBucket> Aggregate(
        IReadOnlyList<LovedTrackPreferences> tracks,
        AggregationThresholds thresholds,
        DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        ArgumentNullException.ThrowIfNull(thresholds);

        var buckets = new List<AggregatedBucket>();

        foreach (var countryGroup in tracks.GroupBy(t => t.Country))
        {
            var countryTracks = countryGroup.ToList();

            if (countryTracks.Count < thresholds.CountryRegionThreshold)
            {
                buckets.Add(AggregatedBucket.Create(
                    bucketName: countryGroup.Key,
                    bucketType: BucketType.Country,
                    country: countryGroup.Key,
                    language: null,
                    genre: null,
                    trackCount: countryTracks.Count,
                    asOfUtc: asOfUtc));
                continue;
            }

            var languageEntries = countryTracks
                .SelectMany(track => track.Languages.Select(language => (Track: track, Language: language)));

            foreach (var languageGroup in languageEntries.GroupBy(entry => entry.Language))
            {
                var tracksForLanguage = languageGroup.Select(entry => entry.Track).ToList();

                if (tracksForLanguage.Count < thresholds.CountryRegionLanguageThreshold)
                {
                    buckets.Add(AggregatedBucket.Create(
                        bucketName: $"{countryGroup.Key}/{languageGroup.Key}",
                        bucketType: BucketType.CountryLanguage,
                        country: countryGroup.Key,
                        language: languageGroup.Key,
                        genre: null,
                        trackCount: tracksForLanguage.Count,
                        asOfUtc: asOfUtc));
                    continue;
                }

                var genreEntries = tracksForLanguage.SelectMany(track => track.Genres);

                foreach (var genreGroup in genreEntries.GroupBy(genre => genre))
                {
                    buckets.Add(AggregatedBucket.Create(
                        bucketName: $"{countryGroup.Key}/{languageGroup.Key}/{genreGroup.Key}",
                        bucketType: BucketType.CountryLanguageGenre,
                        country: countryGroup.Key,
                        language: languageGroup.Key,
                        genre: genreGroup.Key,
                        trackCount: genreGroup.Count(),
                        asOfUtc: asOfUtc));
                }
            }
        }

        return buckets
            .Where(bucket => bucket.TrackCount >= thresholds.MinimumBucketThreshold)
            .ToList();
    }
}
