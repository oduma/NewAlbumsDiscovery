namespace NewAlbumsDiscovery.Domain.MusicAggregator;

/// <summary>
/// Immutable, already-normalized view of a single LovedTracks row's aggregation-relevant fields.
/// Producing this from raw external data (null/empty fallback, JSON parsing) is an Infrastructure
/// concern — by the time an instance exists, Country/Languages/Genres are guaranteed valid, so
/// <see cref="BucketAggregatorEngine"/> never needs to branch on malformed input.
/// </summary>
public sealed class LovedTrackPreferences : IEquatable<LovedTrackPreferences>
{
    public string Country { get; }
    public IReadOnlyList<string> Languages { get; }
    public IReadOnlyList<string> Genres { get; }

    public LovedTrackPreferences(string country, IReadOnlyList<string> languages, IReadOnlyList<string> genres)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            throw new ArgumentException("Country must not be null or empty.", nameof(country));
        }

        Country = country;
        Languages = languages ?? throw new ArgumentNullException(nameof(languages));
        Genres = genres ?? throw new ArgumentNullException(nameof(genres));
    }

    public bool Equals(LovedTrackPreferences? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Country == other.Country
            && Languages.SequenceEqual(other.Languages)
            && Genres.SequenceEqual(other.Genres);
    }

    public override bool Equals(object? obj) => Equals(obj as LovedTrackPreferences);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Country);
        foreach (var language in Languages)
        {
            hash.Add(language);
        }

        foreach (var genre in Genres)
        {
            hash.Add(genre);
        }

        return hash.ToHashCode();
    }
}
