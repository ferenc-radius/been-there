using BeenThere.Core.Domain;
using BeenThere.Core.Interfaces;
using BeenThere.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeenThere.Web.Handlers;

internal static class PreferencesHandlers
{
    internal static async Task<IResult> UpdateStickFigure(
        [FromBody] UpdateStickFigureRequest request,
        ApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        var userId = currentUser.UserId!;

        var prefs = await db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
        if (prefs == null)
        {
            prefs = new UserPreferences { UserId = userId };
            db.UserPreferences.Add(prefs);
        }

        prefs.StickFigure = request.StickFigure;
        await db.SaveChangesAsync();

        return Results.Json(new PreferencesResponse(prefs.StickFigure, prefs.TileProvider ?? "osm"));
    }

    internal static async Task<IResult> GetPreferences(
        ApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        var userId = currentUser.UserId!;

        var prefs = await db.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (prefs == null)
        {
            return Results.Json(new PreferencesResponse("classic", "osm"));
        }

        return Results.Json(new PreferencesResponse(prefs.StickFigure ?? "classic", prefs.TileProvider ?? "osm"));
    }
}

public sealed record UpdateStickFigureRequest(string StickFigure);
public sealed record PreferencesResponse(string StickFigure, string TileProvider);
