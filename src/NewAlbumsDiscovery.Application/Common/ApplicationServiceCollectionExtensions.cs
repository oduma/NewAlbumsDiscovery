using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NewAlbumsDiscovery.Application.AIDiscovery;
using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;
using NewAlbumsDiscovery.Application.CoreOperations;
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

        services.Configure<AIDiscoveryOptions>(configuration.GetSection("NewAlbumsDiscovery:AIDiscovery"));

        // Registration order is load-bearing: IEnumerable<IAIDiscoveryStage> resolves in the exact
        // order stages are registered below (documented Microsoft.Extensions.DependencyInjection
        // behavior), and that order IS the pipeline's execution order (Stage 1 -> Stage 2 -> Stage 3).
        services.AddScoped<IAIDiscoveryStage, StartNotificationStage>();
        services.AddScoped<IAIDiscoveryStage, BucketProcessingStage>();
        services.AddScoped<IAIDiscoveryStage, ReportPublicationStage>();
        services.AddScoped<IBucketProcessingStep, PrintBucketStep>();

        services.Configure<CoreOperationsOptions>(configuration.GetSection("NewAlbumsDiscovery:CoreOperations"));

        return services;
    }
}
