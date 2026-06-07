using BeenThere.Core.Exceptions;
using BeenThere.Core.Interfaces;
using BeenThere.Core.Models;
using BeenThere.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace BeenThere.Web.Handlers;

internal static class RouteHandlers
{
    internal static async Task<IResult> DownloadRoute(
        Guid routeId,
        HttpContext context,
        ICurrentUserService currentUser,
        IDriveService driveService)
    {
        var userId = currentUser.UserId!;

        try
        {
            var fileStream = await driveService.DownloadFileAsync(userId, routeId, context.RequestAborted);
            return Results.File(fileStream, "application/octet-stream", $"route-{routeId}.gpx");
        }
        catch (DriveReauthenticationRequiredException)
        {
            return Results.Problem(
                title: "Google Drive Re-Authentication Required",
                detail: "Google Drive access has expired. Please sign out and sign in again.",
                statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (DriveDownloadException)
        {
            return Results.NotFound();
        }
    }

    /// <summary>Serves routes as GeoJSON for the Leaflet map (ADR-0007).</summary>
    internal static async Task<IResult> GetRoutesGeoJson(ApplicationDbContext db)
    {
        var routes = await db.Routes.AsNoTracking()
            .Select(r => new { r.Id, r.Name, r.Date, Geom = r.Geom })
            .ToListAsync();

        var features = routes.Select(r =>
        {
            object? geometry = null;
            if (r.Geom is LineString ls)
            {
                var coords = ls.Coordinates.Select(c => new[] { c.X, c.Y }).ToArray();
                geometry = new { type = "LineString", coordinates = coords };
            }

            return new { type = "Feature", geometry, properties = new { id = r.Id, name = r.Name, date = r.Date } };
        });

        var fc = new { type = "FeatureCollection", features };
        return Results.Json(fc);
    }

    internal static async Task<IResult> GetRouteDetail(
        Guid routeId,
        HttpContext context,
        ICurrentUserService currentUser,
        IRouteService routeService)
    {
        var userId = currentUser.UserId!;
        var detail = await routeService.GetRouteDetailAsync(userId, routeId, context.RequestAborted);
        return detail is null ? Results.NotFound() : Results.Json(detail);
    }

    internal static async Task<IResult> SearchRoutes(
        double? lat,
        double? lng,
        double? radiusMetres,
        string? placeName,
        int radiusKm,
        double? minLengthM,
        double? maxLengthM,
        string? tag,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        HttpContext context,
        ICurrentUserService currentUser,
        IRouteService routeService)
    {
        var userId = currentUser.UserId!;
        var filter = new RouteSearchFilter(
            Lat: lat,
            Lng: lng,
            RadiusMetres: radiusMetres,
            PlaceName: placeName,
            RadiusKm: radiusKm == 0 ? 10 : radiusKm,
            MinLengthM: minLengthM,
            MaxLengthM: maxLengthM,
            Tag: tag,
            StartDate: startDate,
            EndDate: endDate);
        var results = await routeService.SearchRoutesAsync(userId, filter, context.RequestAborted);
        return Results.Json(results);
    }

    internal static async Task<IResult> UpdateRouteTags(
        Guid routeId,
        TagUpdateDto dto,
        HttpContext context,
        ICurrentUserService currentUser,
        IRouteService routeService)
    {
        var userId = currentUser.UserId!;
        var tags = dto.Tags ?? [];
        var updated = await routeService.UpdateTagsAsync(userId, routeId, tags, context.RequestAborted);
        return updated ? Results.Ok(new { routeId, tags }) : Results.NotFound();
    }

    internal sealed class TagUpdateDto
    {
        public List<string>? Tags { get; set; }
    }

    internal static async Task<IResult> DeleteRoute(
        Guid routeId,
        HttpContext context,
        ICurrentUserService currentUser,
        IRouteService routeService)
    {
        var userId = currentUser.UserId!;
        var deleted = await routeService.DeleteRouteAsync(userId, routeId, context.RequestAborted);
        return deleted ? Results.Ok() : Results.NotFound();
    }

    internal sealed class VisibilityUpdateDto
    {
        public bool IsPublic { get; set; }
    }
}
