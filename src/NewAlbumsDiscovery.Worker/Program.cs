using NewAlbumsDiscovery.Application.Common;
using NewAlbumsDiscovery.Infrastructure;
using NewAlbumsDiscovery.Worker;

// Configuration binding note: Host.CreateDefaultBuilder's default configuration providers already
// include environment variables, so machine-level secrets set via the NewAlbumsDiscovery__Section__Key
// double-underscore convention (see docs/specs/technical-specs.md §2) bind into IConfiguration/
// IOptions<T> without any extra wiring here.

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .UseSystemd()
    .ConfigureServices(services =>
    {
        services
            .AddApplicationServices()
            .AddInfrastructureServices()
            .AddHostedService<HeartbeatWorker>();
    })
    .Build();

host.Run();
