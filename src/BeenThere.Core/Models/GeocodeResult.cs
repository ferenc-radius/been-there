using System.Text.Json.Serialization;

namespace BeenThere.Core.Models;

/// <summary>
/// Geocoding result from Nominatim: a candidate location with coordinates and metadata.
/// </summary>
public class GeocodeResult
{
    [JsonPropertyName("lat")]
    public double Lat { get; init; }

    [JsonPropertyName("lon")]
    public double Lng { get; init; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Optional bounding box: [south, north, west, east] or provider-specific ordering.
    /// </summary>
    public double[]? BoundingBox { get; init; }

    /// <summary>
    /// Provider importance score when available (0.0–1.0).
    /// </summary>
    public double? Importance { get; init; }
}
