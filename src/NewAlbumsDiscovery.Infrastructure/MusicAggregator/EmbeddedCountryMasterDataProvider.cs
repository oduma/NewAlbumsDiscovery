using System.Text.Json;
using NewAlbumsDiscovery.Application.MusicAggregator;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Infrastructure.MusicAggregator;

/// <summary>
/// Reads countries-languages.json (reused from its existing Gemini/Assets location, per Phase 6's
/// DRY decision - not duplicated) and country-synonyms.json (this context's own asset) as embedded
/// resources, parsing each once and caching for the process lifetime. Registered as a singleton, so
/// instance-field caching (not static/Lazy&lt;T&gt;) is sufficient.
/// </summary>
public sealed class EmbeddedCountryMasterDataProvider : ICountryMasterDataProvider
{
    private const string MasterDataResourceName = "NewAlbumsDiscovery.Infrastructure.Gemini.Assets.countries-languages.json";
    private const string SynonymsResourceName = "NewAlbumsDiscovery.Infrastructure.MusicAggregator.Assets.country-synonyms.json";

    private CountryMasterData? _masterData;
    private IReadOnlyDictionary<string, string>? _synonyms;

    public Task<CountryMasterData> GetCountryMasterDataAsync(CancellationToken cancellationToken)
    {
        _masterData ??= BuildMasterData();
        return Task.FromResult(_masterData);
    }

    public Task<IReadOnlyDictionary<string, string>> GetCountrySynonymsAsync(CancellationToken cancellationToken)
    {
        _synonyms ??= ParseJson<Dictionary<string, string>>(SynonymsResourceName);
        return Task.FromResult(_synonyms);
    }

    private static CountryMasterData BuildMasterData()
    {
        var raw = ParseJson<Dictionary<string, RawCountryEntry>>(MasterDataResourceName);
        var entries = raw.ToDictionary(kvp => kvp.Key, kvp => new CountryMasterDataEntry(kvp.Value.Continent));
        return new CountryMasterData(entries);
    }

    private static T ParseJson<T>(string resourceName)
    {
        var assembly = typeof(EmbeddedCountryMasterDataProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");
        return JsonSerializer.Deserialize<T>(stream)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' deserialized to null.");
    }

    private sealed record RawCountryEntry(string Language, string Continent);
}
