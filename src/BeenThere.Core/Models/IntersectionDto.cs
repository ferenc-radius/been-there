namespace BeenThere.Core.Models;

/// <summary>
/// Public DTO for "been-there too" intersection results (contract safe per ADR-0004 & Milestone 6 decision Q10).
/// Shows other users who have overlapped routes.
/// Never exposes internal user IDs; only display names.
/// </summary>
public sealed record IntersectionDto
{
    /// <summary>Display name of the other user who has an overlapping route.</summary>
    public string OtherUserName { get; init; } = string.Empty;

    /// <summary>Name of the other user's route that overlaps.</summary>
    public string RouteName { get; init; } = string.Empty;

    /// <summary>Overlap percentage (0-100): intersection_length / query_route_length * 100.</summary>
    public double OverlapPercentage { get; init; }

    /// <summary>Other user's route ID for deep-link or detail view.</summary>
    public Guid OtherRouteId { get; init; }
}
