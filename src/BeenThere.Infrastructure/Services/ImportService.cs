using BeenThere.Core.Interfaces;
using BeenThere.Core.Models;
using BeenThere.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace BeenThere.Infrastructure.Services;

/// <summary>
/// Thin orchestrator for the import pipeline (ADR-0005, Single Responsibility):
///   1. Select parser (delegated to <see cref="IRouteFileParser"/> implementations)
///   2. Parse stream (delegated to selected parser)
///   3. Upload to Drive (delegated to <see cref="IDriveService"/>)
///   4. Assemble domain objects (delegated to <see cref="IRouteAssembler"/>)
///   5. Persist (one SaveChangesAsync)
///   6. Enqueue duplicate detection (delegated to <see cref="IDuplicateDetectionChannel"/>)
///
/// Adding a new file format requires only a new <see cref="IRouteFileParser"/> implementation
/// registered in DI — this class never changes for that reason (Open/Closed Principle).
/// </summary>
public sealed partial class ImportService(
    IEnumerable<IRouteFileParser> parsers,
    IDriveService driveService,
    IRouteAssembler assembler,
    IDuplicateDetectionChannel duplicateChannel,
    ApplicationDbContext db,
    ILogger<ImportService> logger) : IImportService
{
    public async Task<ImportResult> ImportAsync(
        Stream fileStream,
        string originalFilename,
        string userId,
        CancellationToken ct = default)
    {
        var ext = Path.GetExtension(originalFilename).TrimStart('.').ToLowerInvariant();

        var parser = parsers.FirstOrDefault(p => p.CanHandle(ext));
        if (parser == null)
        {
            return ImportResult.Failure(
                $"Unsupported file format '.{ext}'. Accepted formats: " +
                string.Join(", ", parsers.Select(p => p.GetType().Name)));
        }

        // Buffer into a byte array. MemoryStream is used as a staging area only.
        // We materialise to byte[] because some parsers (e.g. SharpGPX via XmlReader) dispose
        // the stream they receive — a byte[] lets us hand each consumer a fresh MemoryStream.
        using var staging = new MemoryStream();
        await fileStream.CopyToAsync(staging, ct);
        var fileBytes = staging.ToArray();

        ParsedRoute parsed;
        try
        {
            using var parseStream = new MemoryStream(fileBytes);
            parsed = parser.Parse(parseStream, originalFilename);
        }
        catch (Exception ex)
        {
            LogParseWarning(logger, originalFilename, ex);
            return ImportResult.Failure($"Could not parse '{originalFilename}': {ex.Message}");
        }

        if (parsed.Points.Count < 2)
        {
            return ImportResult.Failure(
                $"'{originalFilename}' contains fewer than 2 GPS points and cannot be imported.");
        }

        var routeId = Guid.NewGuid();
        string driveFileId;
        try
        {
            using var uploadStream = new MemoryStream(fileBytes);
            driveFileId = await driveService.UploadFileAsync(
                userId, routeId, parsed.Name, uploadStream, ext, ct);
        }
        catch (Exception ex)
        {
            LogDriveError(logger, originalFilename, ex);
            return ImportResult.Failure(
                $"Failed to store '{originalFilename}' in Drive. Please try again.");
        }

        var (route, routePoints) = assembler.Assemble(parsed, userId, routeId, originalFilename);
        route.DriveFileId = driveFileId;

        try
        {
            db.Routes.Add(route);
            db.RoutePoints.AddRange(routePoints);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            LogDbError(logger, routeId, ex);
            return ImportResult.Failure("Failed to save route data. Please try again.");
        }

        duplicateChannel.Enqueue(routeId);
        LogImportSuccess(logger, routeId, parsed.Name, userId, routePoints.Count);
        return ImportResult.Success(routeId);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse {Filename}")]
    private static partial void LogParseWarning(ILogger logger, string filename, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Drive upload failed for {Filename}")]
    private static partial void LogDriveError(ILogger logger, string filename, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "DB write failed for route {RouteId}")]
    private static partial void LogDbError(ILogger logger, Guid routeId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Imported route {RouteId} '{Name}' for user {UserId} ({Points} points)")]
    private static partial void LogImportSuccess(
        ILogger logger, Guid routeId, string name, string userId, int points);
}
