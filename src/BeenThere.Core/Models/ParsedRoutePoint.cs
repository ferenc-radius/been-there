namespace BeenThere.Core.Models;

/// <summary>
/// A single GPS sample within a parsed route (ADR-0005, ADR-0006).
/// Telemetry fields (HR, cadence, power) are nullable — KML and basic GPX files omit them.
/// </summary>
public sealed class ParsedRoutePoint
{
    /// <summary>Zero-based order within the track.</summary>
    public int Idx { get; init; }

    public double Longitude { get; init; }
    public double Latitude { get; init; }

    public double? ElevationM { get; init; }

    public DateTimeOffset? RecordedAt { get; init; }

    /// <summary>Heart rate in BPM. Extracted from Garmin GPX extensions when present.</summary>
    public short? HrBpm { get; init; }

    /// <summary>Cadence in RPM. Extracted from Garmin GPX extensions when present.</summary>
    public short? CadenceRpm { get; init; }

    /// <summary>Power in Watts. Extracted from Garmin GPX extensions when present.</summary>
    public short? PowerW { get; init; }
}
