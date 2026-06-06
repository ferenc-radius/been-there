using NetTopologySuite.Geometries;

namespace BeenThere.Core.Domain;

/// <summary>
/// Individual GPS sample within a route. Replaces the retired "RouteSample" terminology (ADR-0006).
/// </summary>
public sealed class RoutePoint
{
    public long Id { get; init; }

    public Guid RouteId { get; init; }

    /// <summary>Zero-based order within the track.</summary>
    public int Idx { get; init; }

    public Point Geom { get; init; } = null!;

    public DateTimeOffset? RecordedAt { get; init; }

    public double? ElevationM { get; init; }

    public short? HrBpm { get; init; }

    public short? CadenceRpm { get; init; }

    public short? PowerW { get; init; }

    public Route Route { get; init; } = null!;
}
