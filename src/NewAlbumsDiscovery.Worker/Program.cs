using Microsoft.EntityFrameworkCore;
using NewAlbumsDiscovery.Application.Common;
using NewAlbumsDiscovery.Infrastructure;
using NewAlbumsDiscovery.Infrastructure.Sqlite;
using NewAlbumsDiscovery.Worker;

// Configuration binding note: Host.CreateDefaultBuilder's default configuration providers already
// include environment variables, so machine-level secrets set via the NewAlbumsDiscovery__Section__Key
// double-underscore convention (see docs/specs/technical-specs.md §2) bind into IConfiguration/
// IOptions<T> without any extra wiring here.

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .UseSystemd()
    .ConfigureServices((context, services) =>
    {
        services
            .AddApplicationServices(context.Configuration)
            .AddInfrastructureServices(context.Configuration)
            .AddHostedService<HeartbeatWorker>()
            .AddHostedService<AggregationStartupWorker>();
    })
    .Build();

// Applied once at startup, before any hosted service can touch AppDbContext, so
// new-albums-discovery.db and its AggregatedBuckets table always exist by the time
// AggregationStartupWorker runs (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 3 decision #3).
using (var scope = host.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

host.Run();
