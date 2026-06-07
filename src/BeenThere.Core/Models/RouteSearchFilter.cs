namespace BeenThere.Core.Models;

public sealed record RouteSearchFilter(
    double? Lat = null,
    double? Lng = null,
    double? RadiusMetres = null,
    string? PlaceName = null,
    int RadiusKm = 10,
    double? MinLengthM = null,
    double? MaxLengthM = null,
    string? Tag = null,
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null);
