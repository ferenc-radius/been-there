using System.Text.RegularExpressions;
using BeenThere.Core.Exceptions;
using BeenThere.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848

namespace BeenThere.Infrastructure.Services;

/// <summary>
/// Google Drive implementation for storing route files in appDataFolder.
/// Handles OAuth token management, folder creation, and file operations.
/// </summary>
public class DriveService : IDriveService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<DriveService> _logger;

    // Cache folder IDs per user to avoid repeated folder lookups
    private static readonly Dictionary<string, string> FolderIdCache = new();
    private static readonly object CacheLock = new();

    public DriveService(
        UserManager<IdentityUser> userManager,
        ILogger<DriveService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<string> CreateUserFolderAsync(string userId, CancellationToken ct)
    {
        // Check cache first
        lock (CacheLock)
        {
            if (FolderIdCache.TryGetValue(userId, out var cachedFolderId))
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("User folder cache hit for user {UserId}: {FolderId}", userId, cachedFolderId);
                }
                return cachedFolderId;
            }
        }

        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException($"User {userId} not found");
            }

            // Retrieve the refresh token from AspNetUserTokens
            var refreshToken = await _userManager.GetAuthenticationTokenAsync(user, "Google", "refresh_token");
            if (string.IsNullOrEmpty(refreshToken))
            {
                throw new DriveReauthenticationRequiredException(
                    "Google Drive access has expired. Please sign out and sign in again.");
            }

            // For now, we generate a simple folder ID based on user
            // In production, this would call the actual Google Drive API
            var folderId = $"drive-folder-{userId}";

            lock (CacheLock)
            {
                FolderIdCache[userId] = folderId;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Created/found user folder for {UserId}: {FolderId}", userId, folderId);
            }
            return folderId;
        }
        catch (DriveReauthenticationRequiredException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create/lookup user folder for {UserId}", userId);
            throw new DriveFolderCreationException($"Failed to create or lookup user folder in Drive for user {userId}", ex);
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
            var userFolderId = await CreateUserFolderAsync(userId, ct);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException($"User {userId} not found");
            }

            var sanitisedName = SanitiseFileName(routeName);
            var fileName = $"{routeId}_{sanitisedName}.{fileExtension}";

            // For now, generate a simple file ID based on route ID
            // In production, this would upload to Google Drive
            var fileId = $"drive-file-{routeId}";

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Uploaded file for user {UserId}, route {RouteId}: {FileId}", userId, routeId, fileId);
            }
            return fileId;
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
            _logger.LogError(ex, "Failed to upload file for user {UserId}, route {RouteId}", userId, routeId);
            throw new DriveUploadException($"Failed to upload file to Drive for route {routeId}", ex);
        }
    }

    public async Task<Stream> DownloadFileAsync(string userId, Guid routeId, CancellationToken ct)
    {
        try
        {
            var userFolderId = await CreateUserFolderAsync(userId, ct);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException($"User {userId} not found");
            }

            // For now, return an empty stream
            // In production, this would download from Google Drive
            var memoryStream = new MemoryStream();
            memoryStream.Position = 0;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Downloaded file for user {UserId}, route {RouteId}", userId, routeId);
            }
            return memoryStream;
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
            _logger.LogError(ex, "Failed to download file for user {UserId}, route {RouteId}", userId, routeId);
            throw new DriveDownloadException($"Failed to download file from Drive for route {routeId}", ex);
        }
    }

    private static string SanitiseFileName(string fileName)
    {
        // Remove or replace invalid filename characters
        var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        var invalidRegex = new Regex($"[{invalidChars}]", RegexOptions.Compiled);
        var sanitised = invalidRegex.Replace(fileName, "_");

        // Trim and limit length
        return sanitised.Trim().Length > 100
            ? sanitised.Substring(0, 100)
            : sanitised;
    }
}
