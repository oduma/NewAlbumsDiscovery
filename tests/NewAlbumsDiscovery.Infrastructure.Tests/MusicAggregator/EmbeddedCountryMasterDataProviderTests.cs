using NewAlbumsDiscovery.Infrastructure.MusicAggregator;

namespace NewAlbumsDiscovery.Infrastructure.Tests.MusicAggregator;

/// <summary>
/// Exercises the real embedded countries-languages.json / country-synonyms.json assets, mirroring
/// PromptAssetsTests' style — no mocking, this is the actual data shipping with the assembly.
/// </summary>
public class EmbeddedCountryMasterDataProviderTests
{
    private readonly EmbeddedCountryMasterDataProvider _provider = new();

    [Fact]
    public async Task GetCountryMasterDataAsync_KnownCountry_ResolvesToExpectedContinent()
    {
        var masterData = await _provider.GetCountryMasterDataAsync(CancellationToken.None);

        Assert.True(masterData.IsKnownCountry("USA"));
        var found = masterData.TryGetContinent("USA", out var continent);
        Assert.True(found);
        Assert.Equal("North America", continent);
    }

    [Fact]
    public async Task GetCountryMasterDataAsync_ContinentName_IsRecognizedAsContinent()
    {
        var masterData = await _provider.GetCountryMasterDataAsync(CancellationToken.None);

        Assert.True(masterData.IsContinentName("Europe"));
    }

    [Fact]
    public async Task GetCountryMasterDataAsync_Australia_IsNotRecognizedAsContinent()
    {
        var masterData = await _provider.GetCountryMasterDataAsync(CancellationToken.None);

        Assert.False(masterData.IsContinentName("Australia"));
        Assert.True(masterData.IsKnownCountry("Australia"));
    }

    [Fact]
    public async Task GetCountrySynonymsAsync_ContainsExpectedVariants()
    {
        var synonyms = await _provider.GetCountrySynonymsAsync(CancellationToken.None);

        Assert.Equal("USA", synonyms["United States"]);
        Assert.Equal("UK", synonyms["Great Britain"]);
    }

    [Fact]
    public async Task GetCountryMasterDataAsync_CalledTwice_ReturnsConsistentData()
    {
        var first = await _provider.GetCountryMasterDataAsync(CancellationToken.None);
        var second = await _provider.GetCountryMasterDataAsync(CancellationToken.None);

        Assert.True(first.IsKnownCountry("USA"));
        Assert.True(second.IsKnownCountry("USA"));
    }
}
