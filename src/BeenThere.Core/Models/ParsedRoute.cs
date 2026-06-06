namespace BeenThere.Core.Models;

/// <summary>
/// Normalised output of GPX/KML parsing — format-agnostic intermediate representation.
/// Used as the handoff between parsers and ImportService (ADR-0005).
/// </summary>
public sealed class ParsedRoute
{
    public string Name { get; init; } = string.Empty;

    public DateTimeOffset? Date { get; init; }

    /// <summary>Activity mode inferred from the file (e.g. "hiking", "cycling"). May be null.</summary>
    public string? Mode { get; init; }

    public double DistanceM { get; init; }

    public double ElevGainM { get; init; }

    public IReadOnlyList<ParsedRoutePoint> Points { get; init; } = [];
}
