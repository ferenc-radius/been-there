using BeenThere.Core.Domain;
using BeenThere.Core.Interfaces;
using BeenThere.Core.Models;
using BeenThere.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;


namespace BeenThere.Infrastructure.Services;

internal sealed class RouteService(ApplicationDbContext db, IGeocodingService geocoding) : IRouteService
{
    private readonly ApplicationDbContext _db = db;
    private readonly IGeocodingService _geocoding = geocoding;

    public async Task<IReadOnlyList<Route>> GetRoutesAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.Routes
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteRouteAsync(string userId, Guid routeId, CancellationToken cancellationToken = default)
    {
        var route = await _db.Routes
            .FirstOrDefaultAsync(r => r.Id == routeId && r.UserId == userId, cancellationToken);

        if (route is null)
        {
            return false;
        }

        _db.Routes.Remove(route);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RouteDetailDto?> GetRouteDetailAsync(string userId, Guid routeId, CancellationToken cancellationToken = default)
    {
        return await _db.Routes
            .AsNoTracking()
            .Where(r => r.Id == routeId && r.UserId == userId)
            .Select(r => new RouteDetailDto
            {
                Id = r.Id,
                Name = r.Name,
                Date = r.Date,
                Mode = r.Mode,
                DistanceM = r.DistanceM,
                ElevGainM = r.ElevGainM,
                Tags = r.Tags,
                Notes = r.Notes,
                UserId = r.UserId,
                Points = r.Points
                    .OrderBy(p => p.Idx)
                    .Select(p => new RoutePointSummaryDto(p.Idx, p.ElevationM, p.RecordedAt))
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RouteSearchResult>> SearchRoutesAsync(
        string userId,
        RouteSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Routes.AsNoTracking().Where(r => r.UserId == userId).AsQueryable();

        if (filter.MinLengthM.HasValue)
        {
            query = query.Where(r => r.DistanceM >= filter.MinLengthM.Value);
        }

        if (filter.MaxLengthM.HasValue)
        {
            query = query.Where(r => r.DistanceM <= filter.MaxLengthM.Value);
        }

        if (filter.StartDate.HasValue)
        {
            query = query.Where(r => r.Date >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            query = query.Where(r => r.Date <= filter.EndDate.Value);
        }

        // Place-based search: geocode place name and apply ST_DWithin filter
        if (!string.IsNullOrWhiteSpace(filter.PlaceName))
        {
            var geocodeResults = await _geocoding.GeocodeAsync(filter.PlaceName, maxResults: 1, ct: cancellationToken);
            var firstResult = geocodeResults.FirstOrDefault();
            if (firstResult != null)
            {
                var pt = new Point(firstResult.Lng, firstResult.Lat) { SRID = 4326 };
                var radiusMetres = filter.RadiusKm * 1000.0;
                query = query.Where(r => EF.Functions.IsWithinDistance(r.Centroid!, pt, radiusMetres, true));
            }
            else
            {
                // Place not found; return no results
                return [];
            }
        }
        // Legacy coordinate-based search
        else if (filter.Lat.HasValue && filter.Lng.HasValue && filter.RadiusMetres.HasValue)
        {
            var pt = new Point(filter.Lng.Value, filter.Lat.Value) { SRID = 4326 };
            // IsWithinDistance translates to ST_DWithin, index-eligible on GIST centroid column (ADR-0006)
            query = query.Where(r => EF.Functions.IsWithinDistance(r.Centroid!, pt, filter.RadiusMetres.Value, true));
        }

        var results = await query
            .Select(r => new RouteSearchResult
            {
                Id = r.Id,
                Name = r.Name,
                Date = r.Date,
                Mode = r.Mode,
                DistanceM = r.DistanceM,
                ElevGainM = r.ElevGainM,
                Tags = r.Tags
            })
            .ToListAsync(cancellationToken);

        // Tags use a jsonb value converter that EF Core cannot translate to SQL;
        // apply the containment check in memory after the database round-trip.
        if (!string.IsNullOrEmpty(filter.Tag))
        {
            results = results.Where(r => r.Tags.Contains(filter.Tag)).ToList();
        }

        return results;
    }

    public async Task<bool> UpdateTagsAsync(
        string userId,
        Guid routeId,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default)
    {
        var route = await _db.Routes
            .FirstOrDefaultAsync(r => r.Id == routeId && r.UserId == userId, cancellationToken);

        if (route is null)
        {
            return false;
        }

        route.Tags = [.. tags];
        route.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RouteAnalytics> GetAnalyticsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.Routes
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => new { r.DistanceM, Year = r.Date.Year })
            .ToListAsync(cancellationToken);

        var totalKm = rows.Sum(r => r.DistanceM) / 1000.0;
        var byYear = rows
            .GroupBy(r => r.Year)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.DistanceM) / 1000.0);

        return new RouteAnalytics
        {
            TotalDistanceKm = totalKm,
            TotalRoutes = rows.Count,
            DistanceByYear = byYear
        };
    }
}
