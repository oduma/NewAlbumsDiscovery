using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NewAlbumsDiscovery.Application.AIDiscovery;
using NewAlbumsDiscovery.Application.MusicAggregator;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Application.Common;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ApplicationServiceCollectionExtensions).Assembly));

        services.Configure<AggregatorSettings>(configuration.GetSection("NewAlbumsDiscovery:Aggregator"));
        services.AddSingleton<BucketAggregatorEngine>();
        services.AddSingleton(TimeProvider.System);

        services.Configure<GeminiOptions>(configuration.GetSection("NewAlbumsDiscovery:Gemini"));

        return services;
    }
}
