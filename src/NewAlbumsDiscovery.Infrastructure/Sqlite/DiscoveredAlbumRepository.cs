using Microsoft.EntityFrameworkCore;
using NewAlbumsDiscovery.Application.AIDiscovery;
using NewAlbumsDiscovery.Domain.AIDiscovery;

namespace NewAlbumsDiscovery.Infrastructure.Sqlite;

/// <summary>
/// Additive writes to the internal, read-write new-albums-discovery.db — unlike
/// AggregatedBucketRepository, AddRangeAsync never deletes or replaces existing rows
/// (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 10 → Design Notes).
/// </summary>
public sealed class DiscoveredAlbumRepository : IDiscoveredAlbumRepository
{
    private readonly AppDbContext _dbContext;

    public DiscoveredAlbumRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ISet<AlbumKey>> GetExistingAlbumKeysAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.DiscoveredAlbums
            .AsNoTracking()
            .Select(album => new { album.Artist, album.AlbumName })
            .ToListAsync(cancellationToken);

        return rows.Select(row => AlbumKey.From(row.Artist, row.AlbumName)).ToHashSet();
    }

    public async Task AddRangeAsync(IReadOnlyList<DiscoveredAlbum> albums, CancellationToken cancellationToken)
    {
        _dbContext.DiscoveredAlbums.AddRange(albums);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
