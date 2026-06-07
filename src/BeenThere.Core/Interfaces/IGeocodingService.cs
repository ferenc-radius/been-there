using BeenThere.Core.Models;

namespace BeenThere.Core.Interfaces;

/// <summary>
/// Service contract for geocoding place names to coordinates.
/// </summary>
public interface IGeocodingService
{
    /// <summary>
    /// Resolves a place query to candidate geocoding results.
    /// </summary>
    /// <param name="query">Place name, address, or coordinate-like string.</param>
    /// <param name="maxResults">Maximum candidates to return (default 7).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ordered list of candidates or empty if no match found.</returns>
    Task<IEnumerable<GeocodeResult>> GeocodeAsync(string query, int maxResults = 7, CancellationToken ct = default);
}
