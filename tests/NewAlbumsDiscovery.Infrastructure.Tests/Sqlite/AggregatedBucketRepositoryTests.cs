using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NewAlbumsDiscovery.Domain.MusicAggregator;
using NewAlbumsDiscovery.Infrastructure.Sqlite;

namespace NewAlbumsDiscovery.Infrastructure.Tests.Sqlite;

/// <summary>
/// Exercises AggregatedBucketRepository (and, by using Database.Migrate(), the InitialCreate
/// migration itself) against a real temp-file SQLite database. Each write uses its own fresh
/// DbContext, mirroring the scoped-per-request lifetime the app uses in practice.
/// </summary>
public sealed class AggregatedBucketRepositoryTests : IDisposable
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

    private static AggregatedBucket Bucket(string country, int trackCount)
        => AggregatedBucket.Create(country, BucketType.Country, country, null, null, trackCount, DateTime.UtcNow);

    private async Task<List<AggregatedBucket>> ReadAllAsync()
    {
        await using var context = CreateContext();
        return await context.AggregatedBuckets.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task ReplaceAllAsync_FirstWrite_PersistsAllBuckets()
    {
        await using (var context = CreateContext())
        {
            await new AggregatedBucketRepository(context).ReplaceAllAsync(
                [Bucket("Romania", 5), Bucket("Malta", 2)], CancellationToken.None);
        }

        var stored = await ReadAllAsync();

        Assert.Equal(2, stored.Count);
    }

    [Fact]
    public async Task ReplaceAllAsync_SecondWrite_FullyReplacesFirst()
    {
        await using (var context = CreateContext())
        {
            await new AggregatedBucketRepository(context).ReplaceAllAsync([Bucket("Romania", 5)], CancellationToken.None);
        }

        await using (var context = CreateContext())
        {
            await new AggregatedBucketRepository(context).ReplaceAllAsync([Bucket("Malta", 2)], CancellationToken.None);
        }

        var stored = await ReadAllAsync();

        var bucket = Assert.Single(stored);
        Assert.Equal("Malta", bucket.Country);
    }

    [Fact]
    public async Task ReplaceAllAsync_WithEmptyList_ClearsTable()
    {
        await using (var context = CreateContext())
        {
            await new AggregatedBucketRepository(context).ReplaceAllAsync([Bucket("Romania", 5)], CancellationToken.None);
        }

        await using (var context = CreateContext())
        {
            await new AggregatedBucketRepository(context).ReplaceAllAsync([], CancellationToken.None);
        }

        var stored = await ReadAllAsync();

        Assert.Empty(stored);
    }
}
