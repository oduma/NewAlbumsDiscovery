using Microsoft.EntityFrameworkCore;

namespace NewAlbumsDiscovery.Infrastructure.Sqlite;

/// <summary>
/// Row shape for the external loved-tracks.db's LovedTracks table. Mapped as a keyless entity type
/// (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 3 Design Notes): the real external schema's
/// primary key column/type is unknown to this repo, access is strictly read-only, and only these
/// three columns are ever projected, so identity tracking isn't needed or assumed.
/// </summary>
internal sealed class LovedTrackRow
{
    public string? CountryOrRegion { get; set; }
    public string? LanguagesJson { get; set; }
    public string? GenresJson { get; set; }
}

public sealed class LovedTrackDbContext : DbContext
{
    public LovedTrackDbContext(DbContextOptions<LovedTrackDbContext> options) : base(options)
    {
    }

    internal DbSet<LovedTrackRow> LovedTracks => Set<LovedTrackRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LovedTrackRow>(entity =>
        {
            entity.HasNoKey();
            entity.ToTable("LovedTracks");
        });
    }
}
