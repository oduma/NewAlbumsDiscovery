using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Domain.Tests.MusicAggregator;

public class CountryMasterDataTests
{
    private static CountryMasterData Create() => new(new Dictionary<string, CountryMasterDataEntry>
    {
        ["France"] = new CountryMasterDataEntry("Europe"),
        ["USA"] = new CountryMasterDataEntry("North America"),
        ["Australia"] = new CountryMasterDataEntry("Oceania"),
    });

    [Fact]
    public void Constructor_WithNullDictionary_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CountryMasterData(null!));
    }

    [Fact]
    public void IsKnownCountry_ExactCaseMatch_ReturnsTrue()
    {
        Assert.True(Create().IsKnownCountry("France"));
    }

    [Fact]
    public void IsKnownCountry_DifferentCaseMatch_ReturnsTrue()
    {
        Assert.True(Create().IsKnownCountry("france"));
    }

    [Fact]
    public void IsKnownCountry_UnknownName_ReturnsFalse()
    {
        Assert.False(Create().IsKnownCountry("Narnia"));
    }

    [Fact]
    public void IsContinentName_KnownContinent_ReturnsTrue()
    {
        Assert.True(Create().IsContinentName("Europe"));
    }

    [Fact]
    public void IsContinentName_DifferentCaseMatch_ReturnsTrue()
    {
        Assert.True(Create().IsContinentName("europe"));
    }

    [Fact]
    public void IsContinentName_CountryName_ReturnsFalse()
    {
        Assert.False(Create().IsContinentName("France"));
    }

    [Fact]
    public void IsContinentName_Australia_ReturnsFalse()
    {
        // Australia is a country whose Continent is "Oceania" - the literal string
        // "Australia" must never itself be treated as a continent name.
        Assert.False(Create().IsContinentName("Australia"));
    }

    [Fact]
    public void TryGetContinent_KnownCountry_ReturnsTrueAndContinent()
    {
        var found = Create().TryGetContinent("USA", out var continent);

        Assert.True(found);
        Assert.Equal("North America", continent);
    }

    [Fact]
    public void TryGetContinent_DifferentCaseMatch_ReturnsTrueAndContinent()
    {
        var found = Create().TryGetContinent("usa", out var continent);

        Assert.True(found);
        Assert.Equal("North America", continent);
    }

    [Fact]
    public void TryGetContinent_UnknownName_ReturnsFalseAndNullContinent()
    {
        var found = Create().TryGetContinent("Narnia", out var continent);

        Assert.False(found);
        Assert.Null(continent);
    }
}
