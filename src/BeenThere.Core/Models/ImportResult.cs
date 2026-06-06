namespace BeenThere.Core.Models;

/// <summary>
/// Result returned by IImportService after processing a single file.
/// </summary>
public sealed class ImportResult
{
    private ImportResult() { }

    public bool IsSuccess { get; private init; }
    public Guid? RouteId { get; private init; }
    public string? Error { get; private init; }

    public static ImportResult Success(Guid routeId) =>
        new() { IsSuccess = true, RouteId = routeId };

    public static ImportResult Failure(string error) =>
        new() { IsSuccess = false, Error = error };
}
