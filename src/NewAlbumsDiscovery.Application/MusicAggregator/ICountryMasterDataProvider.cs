using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.MusicAggregator;

public interface ICountryMasterDataProvider
{
    Task<IReadOnlyDictionary<string, string>> GetCountrySynonymsAsync(CancellationToken cancellationToken);

    Task<CountryMasterData> GetCountryMasterDataAsync(CancellationToken cancellationToken);
}
