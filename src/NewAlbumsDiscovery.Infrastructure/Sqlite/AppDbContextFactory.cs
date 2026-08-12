using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NewAlbumsDiscovery.Infrastructure.Sqlite;

/// <summary>
/// Used only by `dotnet ef` design-time tooling (e.g. `migrations add`), which can't build the
/// real Worker DI container since that eagerly validates NewAlbumsDiscovery__Database__LovedTracksDbPath
/// (an env var that legitimately isn't set on every dev machine, especially at design time). The
/// connection string here is never opened for real data — only the model matters for migrations.
/// </summary>
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite("Data Source=design-time-placeholder.db");
        return new AppDbContext(optionsBuilder.Options);
    }
}
