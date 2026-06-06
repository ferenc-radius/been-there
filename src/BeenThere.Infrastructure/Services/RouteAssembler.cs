using BeenThere.Core.Domain;
using BeenThere.Core.Interfaces;
using BeenThere.Core.Models;
using NetTopologySuite.Geometries;

namespace BeenThere.Infrastructure.Services;

/// <summary>
/// Assembles <see cref="Route"/> and <see cref="RoutePoint"/> domain objects from a
/// <see cref="ParsedRoute"/> (Single Responsibility: owns NTS geometry construction
/// and domain object initialisation; <see cref="ImportService"/> owns the workflow).
/// </summary>
public sealed class RouteAssembler : IRouteAssembler
{
    private static readonly GeometryFactory Gf = new(new PrecisionModel(), 4326);

    public (Route Route, IReadOnlyList<RoutePoint> Points) Assemble(
        ParsedRoute parsed,
        string userId,
        Guid routeId,
        string originalFilename)
    {
        var points = BuildRoutePoints(parsed, routeId);
        var lineString = BuildLineString(parsed);
        var centroid = lineString.Centroid;

        var route = new Route
        {
            Id = routeId,
            UserId = userId,
            Name = parsed.Name,
            Date = parsed.Date ?? DateTimeOffset.UtcNow,
            Mode = parsed.Mode,
            DistanceM = parsed.DistanceM,
            ElevGainM = parsed.ElevGainM,
            Geom = lineString,
            Centroid = Gf.CreatePoint(new Coordinate(centroid.X, centroid.Y)),
            OriginalFilename = originalFilename,
        };

        return (route, points);
    }

    private static List<RoutePoint> BuildRoutePoints(ParsedRoute parsed, Guid routeId) =>
        parsed.Points.Select(p => new RoutePoint
        {
            RouteId = routeId,
            Idx = p.Idx,
            Geom = Gf.CreatePoint(new Coordinate(p.Longitude, p.Latitude)),
            ElevationM = p.ElevationM,
            RecordedAt = p.RecordedAt,
            HrBpm = p.HrBpm,
            CadenceRpm = p.CadenceRpm,
            PowerW = p.PowerW,
        }).ToList();

    private static LineString BuildLineString(ParsedRoute parsed)
    {
        var coordinates = parsed.Points
            .Select(p => new Coordinate(p.Longitude, p.Latitude))
            .ToArray();
        return Gf.CreateLineString(coordinates);
    }
}
