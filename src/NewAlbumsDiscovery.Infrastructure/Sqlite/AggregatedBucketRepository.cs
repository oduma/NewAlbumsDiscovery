using Microsoft.EntityFrameworkCore;
using NewAlbumsDiscovery.Application.MusicAggregator;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Infrastructure.Sqlite;

/// <summary>
/// Atomic delete+insert replace, per docs/requirements/phase3-requirements.md §6 — zero downtime,
/// zero risk of a partial write if the run is interrupted.
/// </summary>
public sealed class AggregatedBucketRepository : IAggregatedBucketRepository
{
    private readonly AppDbContext _dbContext;

    public AggregatedBucketRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ReplaceAllAsync(IReadOnlyList<AggregatedBucket> buckets, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM AggregatedBuckets", cancellationToken);

        _dbContext.AggregatedBuckets.AddRange(buckets);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
