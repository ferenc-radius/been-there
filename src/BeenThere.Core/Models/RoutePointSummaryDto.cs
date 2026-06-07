namespace BeenThere.Core.Models;

public sealed record RoutePointSummaryDto(
    int Idx,
    double? ElevationM,
    DateTimeOffset? RecordedAt);
