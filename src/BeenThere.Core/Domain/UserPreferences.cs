namespace BeenThere.Core.Domain;

/// <summary>
/// Per-user UI preferences stored as a single JSON column (ADR-0007, ADR-0010).
/// </summary>
public sealed class UserPreferences
{
    public string UserId { get; init; } = string.Empty;

    /// <summary>Leaflet tile provider: "osm" | "opentopomap".</summary>
    public string TileProvider { get; set; } = "osm";

    /// <summary>Whether the import help accordion has been dismissed by the user.</summary>
    public bool ImportHelpDismissed { get; set; }

    /// <summary>Stick figure style for route markers: "classic" | "waving" | "sitting" | "running".</summary>
    public string StickFigure { get; set; } = "classic";
}
