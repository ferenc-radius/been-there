using BeenThere.Core.Domain;
using BeenThere.Core.Models;

namespace BeenThere.Core.Interfaces;

public interface IRouteService
{
    Task<IReadOnlyList<Route>> GetRoutesAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteRouteAsync(string userId, Guid routeId, CancellationToken cancellationToken = default);
    Task<RouteDetailDto?> GetRouteDetailAsync(string userId, Guid routeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RouteSearchResult>> SearchRoutesAsync(string userId, RouteSearchFilter filter, CancellationToken cancellationToken = default);
    Task<bool> UpdateTagsAsync(string userId, Guid routeId, IReadOnlyList<string> tags, CancellationToken cancellationToken = default);
    Task<RouteAnalytics> GetAnalyticsAsync(string userId, CancellationToken cancellationToken = default);
}
