using System.Text.RegularExpressions;
using BeenThere.Core.Exceptions;
using BeenThere.Core.Interfaces;
using BeenThere.Infrastructure.Drive;
using BeenThere.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BeenThere.Infrastructure.Services;

/// <summary>
/// Stores and retrieves route files in the Google Drive appDataFolder for each user.
/// </summary>
public sealed partial class DriveService : IDriveService
{
    private const string AppDataFolder = "appDataFolder";
    private const string FolderMimeType = "application/vnd.google-apps.folder";
    private const int MaxSanitisedNameLength = 100;

    private static readonly Dictionary<string, string> FolderIdCache = new();
    private static readonly object FolderIdCacheLock = new();
    private static readonly Regex InvalidPathCharsRegex =
        new($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", RegexOptions.Compiled);

    private readonly UserManager<IdentityUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly IGoogleDriveClientFactory _clientFactory;
    private readonly ILogger<DriveService> _logger;

    public DriveService(
        UserManager<IdentityUser> userManager,
        ApplicationDbContext db,
        IGoogleDriveClientFactory clientFactory,
        ILogger<DriveService> logger)
    {
        _userManager = userManager;
        _db = db;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task<string> CreateUserFolderAsync(string userId, CancellationToken ct)
    {
        if (TryGetCachedFolderId(userId, out var cached))
        {
            LogFolderCacheHit(_logger, userId, cached!);
            return cached!;
        }

        try
        {
            var user = await RequireUserAsync(userId);
            var gdrive = await _clientFactory.CreateAsync(user);
            var folderId = await FindOrCreateUserFolderAsync(gdrive, userId, ct);

            CacheFolderId(userId, folderId);
            LogFolderResolved(_logger, userId, folderId);
            return folderId;
        }
        catch (DriveReauthenticationRequiredException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogFolderError(_logger, userId, ex);
            throw new DriveFolderCreationException(
                $"Failed to create or lookup user folder in Drive for user {userId}", ex);
        }
    }

    public async Task<string> UploadFileAsync(
        string userId,
        Guid routeId,
        string routeName,
        Stream fileStream,
        string fileExtension,
        CancellationToken ct)
    {
        try
        {
            var folderId = await CreateUserFolderAsync(userId, ct);
            var user = await RequireUserAsync(userId);
            var gdrive = await _clientFactory.CreateAsync(user);
            var fileName = BuildFileName(routeId, routeName, fileExtension);
            var driveFileId = await UploadToFolderAsync(gdrive, folderId, fileName, fileExtension, fileStream, ct);

            LogUploadSuccess(_logger, fileName, userId, routeId, driveFileId);
            return driveFileId;
        }
        catch (DriveReauthenticationRequiredException)
        {
            throw;
        }
        catch (DriveUploadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogUploadError(_logger, userId, routeId, ex);
            throw new DriveUploadException(
                $"Failed to upload file to Drive for route {routeId}", ex);
        }
    }

    public async Task<Stream> DownloadFileAsync(string userId, Guid routeId, CancellationToken ct)
    {
        try
        {
            var driveFileId = await RequireDriveFileIdAsync(userId, routeId);
            var user = await RequireUserAsync(userId);
            var gdrive = await _clientFactory.CreateAsync(user);
            var stream = await DownloadFromDriveAsync(gdrive, driveFileId, ct);

            LogDownloadSuccess(_logger, driveFileId, userId, routeId);
            return stream;
        }
        catch (DriveReauthenticationRequiredException)
        {
            throw;
        }
        catch (DriveDownloadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogDownloadError(_logger, userId, routeId, ex);
            throw new DriveDownloadException(
                $"Failed to download file from Drive for route {routeId}", ex);
        }
    }

    // ── Folder management ───────────────────────────────────────────────────

    private static async Task<string> FindOrCreateUserFolderAsync(
        Google.Apis.Drive.v3.DriveService gdrive, string userId, CancellationToken ct)
    {
        var folderName = $"beenthere_user_{userId}";
        return await FindFolderAsync(gdrive, folderName, ct)
               ?? await CreateFolderAsync(gdrive, folderName, ct);
    }

    private static async Task<string?> FindFolderAsync(
        Google.Apis.Drive.v3.DriveService gdrive, string folderName, CancellationToken ct)
    {
        var request = gdrive.Files.List();
        request.Spaces = AppDataFolder;
        request.Q = $"name = '{folderName}' and mimeType = '{FolderMimeType}' and trashed = false";
        request.Fields = "files(id)";
        var result = await request.ExecuteAsync(ct);
        return result.Files?.FirstOrDefault()?.Id;
    }

    private static async Task<string> CreateFolderAsync(
        Google.Apis.Drive.v3.DriveService gdrive, string folderName, CancellationToken ct)
    {
        var metadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = folderName,
            Parents = [AppDataFolder],
            MimeType = FolderMimeType
        };
        var request = gdrive.Files.Create(metadata);
        request.Fields = "id";
        var folder = await request.ExecuteAsync(ct);
        return folder.Id;
    }

    // ── File upload ─────────────────────────────────────────────────────────

    private static async Task<string> UploadToFolderAsync(
        Google.Apis.Drive.v3.DriveService gdrive,
        string folderId,
        string fileName,
        string fileExtension,
        Stream fileStream,
        CancellationToken ct)
    {
        var metadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = fileName,
            Parents = [folderId]
        };

        fileStream.Position = 0;
        var mimeType = ResolveMimeType(fileExtension);
        var request = gdrive.Files.Create(metadata, fileStream, mimeType);
        request.Fields = "id";

        var progress = await request.UploadAsync(ct);
        if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
        {
            throw new DriveUploadException(
                $"Google Drive upload failed: {progress.Exception?.Message}",
                progress.Exception!);
        }

        return request.ResponseBody?.Id
               ?? throw new DriveUploadException(
                   "Google Drive upload succeeded but returned no file ID.");
    }

    // ── File download ───────────────────────────────────────────────────────

    private async Task<string> RequireDriveFileIdAsync(string userId, Guid routeId)
    {
        // IgnoreQueryFilters bypasses the global user filter; ownership is
        // enforced explicitly via the UserId == userId predicate.
        var driveFileId = await _db.Routes
            .IgnoreQueryFilters()
            .Where(r => r.Id == routeId && r.UserId == userId)
            .Select(r => r.DriveFileId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(driveFileId))
        {
            throw new DriveDownloadException($"No Drive file found for route {routeId}.");
        }

        return driveFileId;
    }

    private static async Task<Stream> DownloadFromDriveAsync(
        Google.Apis.Drive.v3.DriveService gdrive, string driveFileId, CancellationToken ct)
    {
        var stream = new MemoryStream();
        var request = gdrive.Files.Get(driveFileId);
        await request.DownloadAsync(stream, ct);
        stream.Position = 0;
        return stream;
    }

    // ── Folder ID cache ─────────────────────────────────────────────────────

    private static bool TryGetCachedFolderId(string userId, out string? folderId)
    {
        lock (FolderIdCacheLock)
        {
            return FolderIdCache.TryGetValue(userId, out folderId);
        }
    }

    private static void CacheFolderId(string userId, string folderId)
    {
        lock (FolderIdCacheLock)
        {
            FolderIdCache[userId] = folderId;
        }
    }

    // ── User lookup ─────────────────────────────────────────────────────────

    private async Task<IdentityUser> RequireUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            throw new InvalidOperationException($"User {userId} not found");
        }

        return user;
    }

    // ── File naming ─────────────────────────────────────────────────────────

    private static string BuildFileName(Guid routeId, string routeName, string fileExtension)
    {
        var sanitised = SanitiseName(routeName);
        return $"{routeId}_{sanitised}.{fileExtension}";
    }

    private static string SanitiseName(string name)
    {
        var sanitised = InvalidPathCharsRegex.Replace(name, "_").Trim();
        return sanitised.Length > MaxSanitisedNameLength
            ? sanitised[..MaxSanitisedNameLength]
            : sanitised;
    }

    private static string ResolveMimeType(string fileExtension) =>
        fileExtension.ToLowerInvariant() switch
        {
            "gpx" => "application/gpx+xml",
            "kml" => "application/vnd.google-earth.kml+xml",
            "kmz" => "application/vnd.google-earth.kmz",
            _ => "application/octet-stream"
        };

    // ── Structured log messages ─────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "User folder cache hit for {UserId}: {FolderId}")]
    private static partial void LogFolderCacheHit(ILogger logger, string userId, string folderId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "User folder resolved for {UserId}: {FolderId}")]
    private static partial void LogFolderResolved(ILogger logger, string userId, string folderId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to resolve user folder for {UserId}")]
    private static partial void LogFolderError(ILogger logger, string userId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Uploaded {FileName} for user {UserId}, route {RouteId}: {DriveFileId}")]
    private static partial void LogUploadSuccess(
        ILogger logger, string fileName, string userId, Guid routeId, string driveFileId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Drive upload failed for user {UserId}, route {RouteId}")]
    private static partial void LogUploadError(ILogger logger, string userId, Guid routeId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Downloaded Drive file {DriveFileId} for user {UserId}, route {RouteId}")]
    private static partial void LogDownloadSuccess(
        ILogger logger, string driveFileId, string userId, Guid routeId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Drive download failed for user {UserId}, route {RouteId}")]
    private static partial void LogDownloadError(ILogger logger, string userId, Guid routeId, Exception ex);
}
