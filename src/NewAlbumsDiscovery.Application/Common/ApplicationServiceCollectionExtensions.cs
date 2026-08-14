using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NewAlbumsDiscovery.Application.AIDiscovery;
using NewAlbumsDiscovery.Application.AIDiscovery.Pipeline;
using NewAlbumsDiscovery.Application.CoreOperations;
using NewAlbumsDiscovery.Application.MusicAggregator;
using NewAlbumsDiscovery.Domain.AIDiscovery;
using NewAlbumsDiscovery.Domain.MusicAggregator;
using NewAlbumsDiscovery.Domain.MusicAggregator.Filtering;

namespace NewAlbumsDiscovery.Application.Common;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ApplicationServiceCollectionExtensions).Assembly));

        services.Configure<AggregatorSettings>(configuration.GetSection("NewAlbumsDiscovery:Aggregator"));
        services.AddSingleton<BucketAggregatorEngine>();
        services.AddSingleton<CountryNormalizer>();
        services.AddSingleton(TimeProvider.System);

        // Registration order is load-bearing: IEnumerable<IBucketFilterRule> resolves in this exact
        // order. InvalidCountryDeletionRule must run before ContinentFallbackEliminationRule so
        // continent-fallback evidence is only evaluated against already-validated country buckets.
        services.AddScoped<IBucketFilterRule, InvalidCountryDeletionRule>();
        services.AddScoped<IBucketFilterRule, ContinentFallbackEliminationRule>();

        services.Configure<GeminiOptions>(configuration.GetSection("NewAlbumsDiscovery:Gemini"));

        services.Configure<AIDiscoveryOptions>(configuration.GetSection("NewAlbumsDiscovery:AIDiscovery"));

        services.AddSingleton<PromptRenderer>();
        services.AddSingleton<TimeframeFormatter>();
        services.AddScoped<GeminiRetryExecutor>();

        // Registration order is load-bearing: IEnumerable<IAIDiscoveryStage> resolves in the exact
        // order stages are registered below (documented Microsoft.Extensions.DependencyInjection
        // behavior), and that order IS the pipeline's execution order (Stage 1 -> Stage 2 -> Stage 3).
        services.AddScoped<IAIDiscoveryStage, StartNotificationStage>();
        services.AddScoped<IAIDiscoveryStage, BucketProcessingStage>();
        services.AddScoped<IAIDiscoveryStage, ReportPublicationStage>();

        // Registration order is load-bearing: IEnumerable<IBucketProcessingStep> resolves in this
        // exact order. The standard name/track-count print always runs first, then Prompt 1
        // (genre expansion, no-ops unless the bucket has a Genre), then Prompt 2 (discovery
        // query), then persistence of whatever Prompt 2 discovered (Phase 10) — must run last.
        services.AddScoped<IBucketProcessingStep, PrintBucketStep>();
        services.AddScoped<IBucketProcessingStep, GenreExpansionPromptStep>();
        services.AddScoped<IBucketProcessingStep, DiscoveryQueryPromptStep>();
        services.AddScoped<IBucketProcessingStep, AlbumPersistenceStep>();

        services.Configure<CoreOperationsOptions>(configuration.GetSection("NewAlbumsDiscovery:CoreOperations"));

        return services;
    }
}
