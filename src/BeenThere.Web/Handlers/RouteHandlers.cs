using BeenThere.Core.Exceptions;
using BeenThere.Core.Interfaces;
using BeenThere.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System.Linq;

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

    internal static async Task<IResult> DeleteRoute(
        Guid routeId,
        ApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        var userId = currentUser.UserId!;

        var route = await db.Routes.FirstOrDefaultAsync(r => r.Id == routeId && r.UserId == userId);
        if (route == null)
        {
            return Results.NotFound();
        }

        db.Routes.Remove(route);
        await db.SaveChangesAsync();

        return Results.Ok();
    }
}
