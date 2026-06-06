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

        var fileBytes = await BufferStreamAsync(fileStream, ct);

        if (!TryResolveParser(ext, out var parser))
        {
            return ImportResult.Failure(
                $"Unsupported file format '.{ext}'. Accepted formats: " +
                string.Join(", ", parsers.Select(p => p.GetType().Name)));
        }

        if (!TryParse(parser!, fileBytes, originalFilename, out var parsed, out var parseError))
        {
            return parseError!;
        }

        if (parsed!.Points.Count < 2)
        {
            return ImportResult.Failure(
                $"'{originalFilename}' contains fewer than 2 GPS points and cannot be imported.");
        }

        var routeId = Guid.NewGuid();

        var (driveFileId, uploadError) = await UploadAsync(fileBytes, ext, parsed.Name, userId, routeId, originalFilename, ct);
        if (uploadError != null)
        {
            return uploadError;
        }

        var persistError = await PersistAsync(parsed, userId, routeId, originalFilename, driveFileId!, ct);
        if (persistError != null)
        {
            return persistError;
        }

        duplicateChannel.Enqueue(routeId);
        LogImportSuccess(logger, routeId, parsed.Name, userId, parsed.Points.Count);
        return ImportResult.Success(routeId);
    }

    private bool TryResolveParser(string ext, out IRouteFileParser? parser)
    {
        parser = parsers.FirstOrDefault(p => p.CanHandle(ext));
        return parser != null;
    }

    private static async Task<byte[]> BufferStreamAsync(Stream source, CancellationToken ct)
    {
        // Materialise to byte[] so each consumer (parse, upload) gets a fresh MemoryStream.
        // Some parsers (e.g. SharpGPX via XmlReader) dispose the stream they receive.
        using var staging = new MemoryStream();
        await source.CopyToAsync(staging, ct);
        return staging.ToArray();
    }

    private bool TryParse(
        IRouteFileParser parser,
        byte[] fileBytes,
        string originalFilename,
        out ParsedRoute? parsed,
        out ImportResult? error)
    {
        try
        {
            using var stream = new MemoryStream(fileBytes);
            parsed = parser.Parse(stream, originalFilename);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            LogParseWarning(logger, originalFilename, ex);
            parsed = null;
            error = ImportResult.Failure($"Could not parse '{originalFilename}': {ex.Message}");
            return false;
        }
    }

    private async Task<(string? DriveFileId, ImportResult? Error)> UploadAsync(
        byte[] fileBytes,
        string ext,
        string routeName,
        string userId,
        Guid routeId,
        string originalFilename,
        CancellationToken ct)
    {
        try
        {
            using var stream = new MemoryStream(fileBytes);
            var driveFileId = await driveService.UploadFileAsync(userId, routeId, routeName, stream, ext, ct);
            return (driveFileId, null);
        }
        catch (Exception ex)
        {
            LogDriveError(logger, originalFilename, ex);
            return (null, ImportResult.Failure($"Failed to store '{originalFilename}' in Drive. Please try again."));
        }
    }

    private async Task<ImportResult?> PersistAsync(
        ParsedRoute parsed,
        string userId,
        Guid routeId,
        string originalFilename,
        string driveFileId,
        CancellationToken ct)
    {
        var (route, routePoints) = assembler.Assemble(parsed, userId, routeId, originalFilename);
        route.DriveFileId = driveFileId;

        try
        {
            db.Routes.Add(route);
            db.RoutePoints.AddRange(routePoints);
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (Exception ex)
        {
            LogDbError(logger, routeId, ex);
            return ImportResult.Failure("Failed to save route data. Please try again.");
        }
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
