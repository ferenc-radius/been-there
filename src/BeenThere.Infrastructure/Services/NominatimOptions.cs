namespace BeenThere.Infrastructure.Services;

/// <summary>
/// Configuration options for Nominatim geocoding service.
/// </summary>
public class NominatimOptions
{
    public string BaseUrl { get; init; } = "https://nominatim.openstreetmap.org/";
    public string? UserAgent { get; init; }
    public double RateLimitPerSecond { get; init; } = 1.0;
    public int CacheTtlSeconds { get; init; } = 3600;
    public int MaxRetries { get; init; } = 3;
}
