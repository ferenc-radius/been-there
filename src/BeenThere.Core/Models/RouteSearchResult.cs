namespace BeenThere.Core.Models;

public sealed class RouteSearchResult
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTimeOffset Date { get; init; }
    public string? Mode { get; init; }
    public double DistanceM { get; init; }
    public double ElevGainM { get; init; }
    public List<string> Tags { get; init; } = [];
}
