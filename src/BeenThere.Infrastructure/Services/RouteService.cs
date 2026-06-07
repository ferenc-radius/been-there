using BeenThere.Core.Domain;
using BeenThere.Core.Interfaces;
using BeenThere.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BeenThere.Infrastructure.Services;

internal sealed class RouteService : IRouteService
{
    private readonly ApplicationDbContext _db;

    public RouteService(ApplicationDbContext db)
    {
        _db = db;
    }

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

        if (route is null) return false;

        _db.Routes.Remove(route);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
