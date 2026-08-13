using Microsoft.EntityFrameworkCore;
using NewAlbumsDiscovery.Application.AIDiscovery;
using NewAlbumsDiscovery.Domain.AIDiscovery;

namespace NewAlbumsDiscovery.Infrastructure.Sqlite;

/// <summary>
/// Additive persistence — unlike AggregatedBucketRepository.ReplaceAllAsync, AddRangeAsync never
/// deletes existing rows, since discovered albums accumulate over time.
/// </summary>
public sealed class DiscoveredAlbumRepository : IDiscoveredAlbumRepository
{
    private readonly AppDbContext _dbContext;

    public DiscoveredAlbumRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<DiscoveredAlbum>> GetAllAsync(CancellationToken cancellationToken)
        => await _dbContext.DiscoveredAlbums.AsNoTracking().ToListAsync(cancellationToken);

    public async Task AddRangeAsync(IReadOnlyList<DiscoveredAlbum> albums, CancellationToken cancellationToken)
    {
        _dbContext.DiscoveredAlbums.AddRange(albums);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
