using BeenThere.Core.Domain;

namespace BeenThere.Core.Interfaces;

public interface IRouteService
{
    Task<IReadOnlyList<Route>> GetRoutesAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteRouteAsync(string userId, Guid routeId, CancellationToken cancellationToken = default);
}
