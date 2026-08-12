using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NewAlbumsDiscovery.Application.MusicAggregator;
using NewAlbumsDiscovery.Domain.MusicAggregator;

namespace NewAlbumsDiscovery.Infrastructure.Sqlite;

/// <summary>
/// Normalizes raw LovedTracks rows into Domain-valid LovedTrackPreferences at the Infrastructure
/// boundary (docs/requirements/FUNCTIONAL_REQUIREMENTS.md → Phase 3 decision #2): null/empty
/// CountryOrRegion becomes "Unknown"; null or malformed LanguagesJson/GenresJson becomes an empty
/// list. Never throws on bad source data, never skips a row.
/// </summary>
public sealed class LovedTrackRepository : ILovedTrackRepository
{
    private const string UnknownCountry = "Unknown";

    private readonly LovedTrackDbContext _dbContext;

    public LovedTrackRepository(LovedTrackDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<LovedTrackPreferences>> GetAllAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.LovedTracks.AsNoTracking().ToListAsync(cancellationToken);

        return rows
            .Select(row => new LovedTrackPreferences(
                NormalizeCountry(row.CountryOrRegion),
                ParseStringArray(row.LanguagesJson),
                ParseStringArray(row.GenresJson)))
            .ToList();
    }

    private static string NormalizeCountry(string? countryOrRegion)
        => string.IsNullOrWhiteSpace(countryOrRegion) ? UnknownCountry : countryOrRegion;

    private static IReadOnlyList<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
