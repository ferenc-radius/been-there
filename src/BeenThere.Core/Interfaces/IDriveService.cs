namespace BeenThere.Core.Interfaces;

/// <summary>
/// Abstraction for Google Drive operations.
/// Implementations handle OAuth token management, file upload/download, and folder management.
/// </summary>
public interface IDriveService
{
    /// <summary>
    /// Creates a per-user folder inside appDataFolder if it doesn't exist.
    /// Idempotent: subsequent calls return the existing folder ID.
    /// </summary>
    /// <param name="userId">The user's ID (used to name and locate the Drive folder).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The Drive file ID of the user's app data folder.</returns>
    /// <exception cref="DriveFolderCreationException">Thrown if folder creation or lookup fails.</exception>
    Task<string> CreateUserFolderAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Uploads a file to the user's Drive folder.
    /// File is named according to the convention: {routeId}_{sanitised-route-name}.{ext}
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="routeId">The route's unique ID (used in file naming).</param>
    /// <param name="routeName">The route's display name (sanitised for file naming).</param>
    /// <param name="fileStream">The file content to upload.</param>
    /// <param name="fileExtension">File extension without the dot (e.g., "gpx", "kml").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The Drive file ID of the uploaded file.</returns>
    /// <exception cref="DriveUploadException">Thrown if the upload fails.</exception>
    Task<string> UploadFileAsync(
        string userId,
        Guid routeId,
        string routeName,
        Stream fileStream,
        string fileExtension,
        CancellationToken ct);

    /// <summary>
    /// Downloads a file from Drive by route ID (internal lookup; driveFileId is never exposed).
    /// The caller is responsible for disposing the returned stream.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="routeId">The route's unique ID (used to look up the Drive file).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A stream of the file content ready for reading.</returns>
    /// <exception cref="DriveDownloadException">Thrown if the download fails or the file is not found.</exception>
    Task<Stream> DownloadFileAsync(string userId, Guid routeId, CancellationToken ct);
}
