using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NewAlbumsDiscovery.Infrastructure.Sqlite;

namespace NewAlbumsDiscovery.Infrastructure.Tests.Sqlite;

/// <summary>
/// Exercises LovedTrackRepository against a real temp-file SQLite database (not mocked) — this
/// repository IS the isolation boundary the constitution asks for, so it's tested against the real
/// thing rather than a fake.
/// </summary>
public sealed class LovedTrackRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"loved-tracks-test-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    private async Task SeedAsync(params (string? Country, string? LanguagesJson, string? GenresJson)[] rows)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE LovedTracks (CountryOrRegion TEXT, LanguagesJson TEXT, GenresJson TEXT)";
            await create.ExecuteNonQueryAsync();
        }

        foreach (var row in rows)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO LovedTracks (CountryOrRegion, LanguagesJson, GenresJson) VALUES ($country, $languages, $genres)";
            insert.Parameters.AddWithValue("$country", (object?)row.Country ?? DBNull.Value);
            insert.Parameters.AddWithValue("$languages", (object?)row.LanguagesJson ?? DBNull.Value);
            insert.Parameters.AddWithValue("$genres", (object?)row.GenresJson ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync();
        }
    }

    private LovedTrackDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LovedTrackDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new LovedTrackDbContext(options);
    }

    [Fact]
    public async Task GetAllAsync_WithValidRow_MapsCorrectly()
    {
        await SeedAsync(("Romania", "[\"English\"]", "[\"Rock\",\"Pop\"]"));

        await using var context = CreateContext();
        var result = await new LovedTrackRepository(context).GetAllAsync(CancellationToken.None);

        var preference = Assert.Single(result);
        Assert.Equal("Romania", preference.Country);
        Assert.Equal(["English"], preference.Languages);
        Assert.Equal(["Rock", "Pop"], preference.Genres);
    }

    [Fact]
    public async Task GetAllAsync_WithNullCountry_NormalizesToUnknown()
    {
        await SeedAsync((null, "[]", "[]"));

        await using var context = CreateContext();
        var result = await new LovedTrackRepository(context).GetAllAsync(CancellationToken.None);

        Assert.Equal("Unknown", Assert.Single(result).Country);
    }

    [Fact]
    public async Task GetAllAsync_WithEmptyCountry_NormalizesToUnknown()
    {
        await SeedAsync(("   ", "[]", "[]"));

        await using var context = CreateContext();
        var result = await new LovedTrackRepository(context).GetAllAsync(CancellationToken.None);

        Assert.Equal("Unknown", Assert.Single(result).Country);
    }

    [Fact]
    public async Task GetAllAsync_WithMalformedLanguagesJson_ReturnsEmptyList()
    {
        await SeedAsync(("Spain", "not-json", "[]"));

        await using var context = CreateContext();
        var result = await new LovedTrackRepository(context).GetAllAsync(CancellationToken.None);

        Assert.Empty(Assert.Single(result).Languages);
    }

    [Fact]
    public async Task GetAllAsync_WithMalformedGenresJson_ReturnsEmptyList()
    {
        await SeedAsync(("Spain", "[]", "{not valid"));

        await using var context = CreateContext();
        var result = await new LovedTrackRepository(context).GetAllAsync(CancellationToken.None);

        Assert.Empty(Assert.Single(result).Genres);
    }

    [Fact]
    public async Task GetAllAsync_WithNullJsonColumns_ReturnsEmptyLists()
    {
        await SeedAsync(("Spain", null, null));

        await using var context = CreateContext();
        var result = await new LovedTrackRepository(context).GetAllAsync(CancellationToken.None);

        var preference = Assert.Single(result);
        Assert.Empty(preference.Languages);
        Assert.Empty(preference.Genres);
    }

    [Fact]
    public async Task GetAllAsync_WithEmptyTable_ReturnsEmptyList()
    {
        await SeedAsync();

        await using var context = CreateContext();
        var result = await new LovedTrackRepository(context).GetAllAsync(CancellationToken.None);

        Assert.Empty(result);
    }
}
