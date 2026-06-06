using BeenThere.Core.Domain;
using BeenThere.Core.Models;

namespace BeenThere.Core.Interfaces;

/// <summary>
/// Assembles a <see cref="Route"/> and its <see cref="RoutePoint"/> list from a parsed route
/// (Single Responsibility: domain object construction is not the import orchestrator's concern).
/// </summary>
public interface IRouteAssembler
{
    /// <summary>
    /// Builds a <see cref="Route"/> and its <see cref="RoutePoint"/> list from a
    /// <see cref="ParsedRoute"/>. Computes NTS geometry (LineString + centroid).
    /// Does not persist anything.
    /// </summary>
    /// <param name="parsed">Normalised parser output.</param>
    /// <param name="userId">The importing user's identity.</param>
    /// <param name="routeId">Pre-allocated route ID (caller owns ID generation).</param>
    /// <param name="originalFilename">Stored on the route for provenance.</param>
    (Route Route, IReadOnlyList<RoutePoint> Points) Assemble(
        ParsedRoute parsed,
        string userId,
        Guid routeId,
        string originalFilename);
}
