using NetTopologySuite.Geometries;

namespace BeenThere.Core.Domain;

public sealed class Route
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserId { get; init; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset Date { get; set; }

    /// <summary>Activity mode: e.g. "hiking", "cycling", "running".</summary>
    public string? Mode { get; set; }

    public double DistanceM { get; set; }

    public double ElevGainM { get; set; }

    /// <summary>Route geometry as a LineString in SRID 4326. Derived from RoutePoints on import.</summary>
    public LineString? Geom { get; set; }

    /// <summary>Centroid of the route geometry in SRID 4326. Used for fast proximity pre-filter.</summary>
    public Point? Centroid { get; set; }

    /// <summary>Google Drive file ID for the original GPX/KML. Internal — never exposed in DTOs.</summary>
    internal string? DriveFileId { get; set; }

    public string? OriginalFilename { get; set; }

    /// <summary>Free-form tags stored as a JSON array.</summary>
    public List<string> Tags { get; set; } = [];

    public string? Notes { get; set; }

    /// <summary>Set by the duplicate-detection background job when Hausdorff distance ≤ threshold.</summary>
    public bool IsPotentialDuplicate { get; set; }

    public Guid? DuplicateOfId { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<RoutePoint> Points { get; init; } = [];

    public ICollection<RouteRating> Ratings { get; init; } = [];

    public ICollection<RouteReview> Reviews { get; init; } = [];
}
