using BeenThere.Core.Domain;
using BeenThere.Core.Interfaces;
using BeenThere.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BeenThere.Infrastructure.Services;

internal sealed class PreferencesService : IPreferencesService
{
    private readonly ApplicationDbContext _db;

    public PreferencesService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UserPreferencesDto> GetPreferencesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var prefs = await _db.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        return prefs is null
            ? new UserPreferencesDto("classic", "osm")
            : new UserPreferencesDto(prefs.StickFigure ?? "classic", prefs.TileProvider ?? "osm");
    }

    public async Task UpdateStickFigureAsync(string userId, string figureKey, CancellationToken cancellationToken = default)
    {
        var prefs = await _db.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (prefs is null)
        {
            prefs = new UserPreferences { UserId = userId };
            _db.UserPreferences.Add(prefs);
        }

        prefs.StickFigure = figureKey;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
