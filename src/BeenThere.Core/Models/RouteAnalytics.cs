namespace BeenThere.Core.Models;

public sealed class RouteAnalytics
{
    public double TotalDistanceKm { get; init; }
    public int TotalRoutes { get; init; }

    /// <summary>Distance in km per calendar year, ordered by year ascending.</summary>
    public IReadOnlyDictionary<int, double> DistanceByYear { get; init; } = new Dictionary<int, double>();
}
