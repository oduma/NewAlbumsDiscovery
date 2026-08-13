namespace NewAlbumsDiscovery.Domain.MusicAggregator;

/// <summary>
/// Case-insensitive lookup surface over the countries-languages.json master data, used by the
/// Phase 6 <see cref="Filtering.IBucketFilterRule"/> implementations. Precomputes the distinct set
/// of continent names once so both country and continent membership checks are O(1) per bucket.
/// </summary>
public sealed class CountryMasterData
{
    private readonly IReadOnlyDictionary<string, CountryMasterDataEntry> _countries;
    private readonly IReadOnlySet<string> _continents;

    public CountryMasterData(IReadOnlyDictionary<string, CountryMasterDataEntry> countries)
    {
        ArgumentNullException.ThrowIfNull(countries);
        _countries = new Dictionary<string, CountryMasterDataEntry>(countries, StringComparer.OrdinalIgnoreCase);
        _continents = _countries.Values.Select(e => e.Continent).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsKnownCountry(string name) => _countries.ContainsKey(name);

    public bool IsContinentName(string name) => _continents.Contains(name);

    public bool TryGetContinent(string country, out string? continent)
    {
        if (_countries.TryGetValue(country, out var entry))
        {
            continent = entry.Continent;
            return true;
        }

        continent = null;
        return false;
    }
}
