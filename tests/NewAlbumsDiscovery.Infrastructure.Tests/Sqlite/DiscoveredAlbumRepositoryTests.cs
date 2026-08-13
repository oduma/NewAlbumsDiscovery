using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Infrastructure.Sqlite;

namespace NewAlbumsDiscovery.Infrastructure.Tests.Sqlite;

/// <summary>
/// Exercises DiscoveredAlbumRepository (and, by using Database.Migrate(), the AddDiscoveredAlbums
/// migration) against a real temp-file SQLite database — same pattern as AggregatedBucketRepositoryTests.
/// </summary>
public sealed class DiscoveredAlbumRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"app-db-test-{Guid.NewGuid():N}.db");
    private static readonly DateTime DiscoveredAtUtc = new(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        var context = new AppDbContext(options);
        context.Database.Migrate();
        return context;
    }

    private static DiscoveredAlbum Album(string artist, string album, string? language = null, string? genre = null)
        => DiscoveredAlbum.Create(artist, album, "Romania", language, genre, DiscoveredAtUtc);

    [Fact]
    public async Task AddRangeAsync_OnEmptyTable_PersistsAllRows()
    {
        await using (var context = CreateContext())
        {
            await new DiscoveredAlbumRepository(context).AddRangeAsync(
                [Album("Artist A", "Album A"), Album("Artist B", "Album B")], CancellationToken.None);
        }

        await using var readContext = CreateContext();
        var stored = await readContext.DiscoveredAlbums.AsNoTracking().ToListAsync();

        Assert.Equal(2, stored.Count);
    }

    [Fact]
    public async Task AddRangeAsync_CalledTwice_AddsToExistingRowsRatherThanReplacing()
    {
        await using (var context = CreateContext())
        {
            await new DiscoveredAlbumRepository(context).AddRangeAsync([Album("Artist A", "Album A")], CancellationToken.None);
        }

        await using (var context = CreateContext())
        {
            await new DiscoveredAlbumRepository(context).AddRangeAsync([Album("Artist B", "Album B")], CancellationToken.None);
        }

        await using var readContext = CreateContext();
        var stored = await readContext.DiscoveredAlbums.AsNoTracking().ToListAsync();

        Assert.Equal(2, stored.Count);
    }

    [Fact]
    public async Task GetAllAsync_RoundTripsAllFieldsIncludingNullableLanguageAndGenre()
    {
        await using (var context = CreateContext())
        {
            await new DiscoveredAlbumRepository(context).AddRangeAsync(
                [Album("Artist A", "Album A", "Romanian", "Rock")], CancellationToken.None);
        }

        await using var readContext = CreateContext();
        var stored = await new DiscoveredAlbumRepository(readContext).GetAllAsync(CancellationToken.None);

        var album = Assert.Single(stored);
        Assert.Equal("Artist A", album.Artist);
        Assert.Equal("Album A", album.Album);
        Assert.Equal("Romania", album.Country);
        Assert.Equal("Romanian", album.Language);
        Assert.Equal("Rock", album.Genre);
        Assert.Equal(DiscoveredAtUtc, album.DiscoveredAtUtc);
    }

    [Fact]
    public async Task GetAllAsync_WithNullLanguageAndGenre_RoundTripsAsNull()
    {
        await using (var context = CreateContext())
        {
            await new DiscoveredAlbumRepository(context).AddRangeAsync([Album("Artist A", "Album A")], CancellationToken.None);
        }

        await using var readContext = CreateContext();
        var stored = await new DiscoveredAlbumRepository(readContext).GetAllAsync(CancellationToken.None);

        var album = Assert.Single(stored);
        Assert.Null(album.Language);
        Assert.Null(album.Genre);
    }

    [Fact]
    public async Task GetAllAsync_WithEmptyTable_ReturnsEmptyList()
    {
        await using var context = CreateContext();
        var stored = await new DiscoveredAlbumRepository(context).GetAllAsync(CancellationToken.None);

        Assert.Empty(stored);
    }
}
