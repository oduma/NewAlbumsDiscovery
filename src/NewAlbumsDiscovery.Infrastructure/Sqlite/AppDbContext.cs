using Microsoft.EntityFrameworkCore;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Infrastructure.Sqlite;

/// <summary>
/// The internal, read-write new-albums-discovery.db, owned exclusively by this application and
/// maintained via EF Core Migrations. Maps directly onto the Domain entities (no separate
/// Infrastructure persistence model) via their single constructor — EF Core's constructor binding
/// matches parameter names to property names, so read-only auto-properties materialize correctly
/// without needing backing-field access.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AggregatedBucket> AggregatedBuckets => Set<AggregatedBucket>();

    public DbSet<DiscoveredAlbum> DiscoveredAlbums => Set<DiscoveredAlbum>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AggregatedBucket>(entity =>
        {
            entity.ToTable("AggregatedBuckets");
            entity.HasKey(bucket => bucket.Id);
            entity.Property(bucket => bucket.BucketName).IsRequired();
            entity.Property(bucket => bucket.BucketType).HasConversion<string>().IsRequired();
            entity.Property(bucket => bucket.Country).IsRequired();
            entity.Property(bucket => bucket.Language);
            entity.Property(bucket => bucket.Genre);
            entity.Property(bucket => bucket.TrackCount).IsRequired();
            entity.Property(bucket => bucket.CreatedAtUtc).IsRequired();
        });

        // FK cascades on delete deliberately (docs/requirements/FUNCTIONAL_REQUIREMENTS.md ->
        // Phase 10 -> Decision 4): AggregatedBucketRepository.ReplaceAllAsync wipes and reinserts
        // every AggregatedBuckets row on every run, so DiscoveredAlbums rows from a prior run are
        // cascade-deleted the next time the aggregator runs - an accepted tradeoff, not a bug.
        modelBuilder.Entity<DiscoveredAlbum>(entity =>
        {
            entity.ToTable("DiscoveredAlbums");
            entity.HasKey(album => album.Id);
            entity.Property(album => album.Artist).IsRequired();
            entity.Property(album => album.AlbumName).IsRequired();
            entity.Property(album => album.Status).HasConversion<string>().IsRequired();
            entity.Property(album => album.DiscoveredAtUtc).IsRequired();
            entity.HasOne<AggregatedBucket>()
                .WithMany()
                .HasForeignKey(album => album.ReferenceBucketId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
