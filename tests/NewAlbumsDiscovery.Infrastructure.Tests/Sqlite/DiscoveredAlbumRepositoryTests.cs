using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;
using NewAlbumsDiscovery.Infrastructure.Sqlite;

namespace NewAlbumsDiscovery.Infrastructure.Tests.Sqlite;

/// <summary>
/// Exercises DiscoveredAlbumRepository (and, by using Database.Migrate(), the AddDiscoveredAlbums
/// migration itself) against a real temp-file SQLite database, mirroring
/// AggregatedBucketRepositoryTests. Each write uses its own fresh DbContext, mirroring the
/// scoped-per-request lifetime the app uses in practice.
/// </summary>
public sealed class DiscoveredAlbumRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"app-db-test-{Guid.NewGuid():N}.db");

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

    private static AggregatedBucket Bucket(string name)
        => AggregatedBucket.Create(name, BucketType.Country, name, null, null, 5, DateTime.UtcNow);

    [Fact]
    public async Task GetExistingAlbumKeysAsync_OnEmptyTable_ReturnsEmptySet()
    {
        await using var context = CreateContext();

        var keys = await new DiscoveredAlbumRepository(context).GetExistingAlbumKeysAsync(CancellationToken.None);

        Assert.Empty(keys);
    }

    [Fact]
    public async Task AddRangeAsync_ThenGetExistingAlbumKeysAsync_ReflectsPersistedRowsNormalized()
    {
        Guid bucketId;
        await using (var context = CreateContext())
        {
            var bucket = Bucket("Romania");
            context.AggregatedBuckets.Add(bucket);
            await context.SaveChangesAsync();
            bucketId = bucket.Id;

            await new DiscoveredAlbumRepository(context).AddRangeAsync(
                [DiscoveredAlbum.Create(bucketId, "Daft Punk", "Discovery", DateTime.UtcNow)], CancellationToken.None);
        }

        await using var readContext = CreateContext();
        var keys = await new DiscoveredAlbumRepository(readContext).GetExistingAlbumKeysAsync(CancellationToken.None);

        Assert.Contains(AlbumKey.From("daft punk", "discovery"), keys);
    }

    [Fact]
    public async Task AddRangeAsync_CalledTwice_BothPersistAdditively()
    {
        Guid bucketId;
        await using (var context = CreateContext())
        {
            var bucket = Bucket("Romania");
            context.AggregatedBuckets.Add(bucket);
            await context.SaveChangesAsync();
            bucketId = bucket.Id;
        }

        await using (var context = CreateContext())
        {
            await new DiscoveredAlbumRepository(context).AddRangeAsync(
                [DiscoveredAlbum.Create(bucketId, "Daft Punk", "Discovery", DateTime.UtcNow)], CancellationToken.None);
        }

        await using (var context = CreateContext())
        {
            await new DiscoveredAlbumRepository(context).AddRangeAsync(
                [DiscoveredAlbum.Create(bucketId, "Justice", "Cross", DateTime.UtcNow)], CancellationToken.None);
        }

        await using var readContext = CreateContext();
        var keys = await new DiscoveredAlbumRepository(readContext).GetExistingAlbumKeysAsync(CancellationToken.None);

        Assert.Equal(2, keys.Count);
    }

    [Fact]
    public async Task DeletingReferencedBucket_CascadesToDeleteDependentDiscoveredAlbums()
    {
        Guid bucketId;
        await using (var context = CreateContext())
        {
            var bucket = Bucket("Romania");
            context.AggregatedBuckets.Add(bucket);
            await context.SaveChangesAsync();
            bucketId = bucket.Id;

            await new DiscoveredAlbumRepository(context).AddRangeAsync(
                [DiscoveredAlbum.Create(bucketId, "Daft Punk", "Discovery", DateTime.UtcNow)], CancellationToken.None);
        }

        await using (var context = CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM AggregatedBuckets");
        }

        await using var readContext = CreateContext();
        var keys = await new DiscoveredAlbumRepository(readContext).GetExistingAlbumKeysAsync(CancellationToken.None);

        Assert.Empty(keys);
    }
}
