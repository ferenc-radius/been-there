using BeenThere.Core.Models;

namespace BeenThere.Core.Interfaces;

/// <summary>
/// Orchestrates file import: parse → Drive upload → DB persist → enqueue duplicate check (ADR-0005).
/// </summary>
public interface IImportService
{
    /// <summary>
    /// Imports a single GPX or KML file for the given user.
    /// </summary>
    /// <param name="fileStream">The file content. Not disposed by the service.</param>
    /// <param name="originalFilename">Original filename including extension (.gpx or .kml).</param>
    /// <param name="userId">Identity user ID of the importing user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>ImportResult.Success with the new routeId, or ImportResult.Failure with a user-facing error message.</returns>
    Task<ImportResult> ImportAsync(
        Stream fileStream,
        string originalFilename,
        string userId,
        CancellationToken ct = default);
}
