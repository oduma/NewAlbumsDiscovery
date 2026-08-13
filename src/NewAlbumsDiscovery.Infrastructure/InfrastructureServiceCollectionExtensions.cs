using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NewAlbumsDiscovery.Application.AIDiscovery;
using NewAlbumsDiscovery.Application.MusicAggregator;
using NewAlbumsDiscovery.Infrastructure.Gemini;
using NewAlbumsDiscovery.Infrastructure.Sqlite;

namespace NewAlbumsDiscovery.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var lovedTracksDbPath = configuration["NewAlbumsDiscovery:Database:LovedTracksDbPath"];
        if (string.IsNullOrWhiteSpace(lovedTracksDbPath))
        {
            throw new InvalidOperationException(
                "NewAlbumsDiscovery__Database__LovedTracksDbPath is not set. It must point at the externally-owned loved-tracks.db.");
        }

        if (!File.Exists(lovedTracksDbPath))
        {
            throw new InvalidOperationException(
                $"loved-tracks.db was not found at '{lovedTracksDbPath}'. It is owned by an external product and is never created by this application.");
        }

        var appDbPath = configuration["NewAlbumsDiscovery:Database:AppDbPath"];
        if (string.IsNullOrWhiteSpace(appDbPath))
        {
            appDbPath = Path.Combine(AppContext.BaseDirectory, "Database", "new-albums-discovery.db");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(appDbPath)!);

        services.AddDbContext<LovedTrackDbContext>(options =>
            options.UseSqlite($"Data Source={lovedTracksDbPath};Mode=ReadOnly"));

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={appDbPath}"));

        services.AddScoped<ILovedTrackRepository, LovedTrackRepository>();
        services.AddScoped<IAggregatedBucketRepository, AggregatedBucketRepository>();

        var geminiApiKey = configuration["NewAlbumsDiscovery:GeminiApiKey"];
        if (string.IsNullOrWhiteSpace(geminiApiKey))
        {
            throw new InvalidOperationException(
                "NewAlbumsDiscovery__GeminiApiKey is not set. It must contain a valid Gemini API key.");
        }

        services.AddScoped<IGeminiApiClient>(sp =>
            new GeminiApiClient(geminiApiKey, sp.GetRequiredService<IOptions<GeminiOptions>>().Value.Model));
        services.AddScoped<IGeminiDiscoveryClient, GeminiDiscoveryClient>();
        services.AddScoped<IDiscoveredAlbumRepository, DiscoveredAlbumRepository>();

        return services;
    }
}
