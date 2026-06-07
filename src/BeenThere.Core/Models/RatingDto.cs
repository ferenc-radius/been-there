namespace BeenThere.Core.Models;

/// <summary>
/// Public DTO for route ratings (contract safe per ADR-0004 & Milestone 6 decision Q10).
/// Never exposes internal user IDs.
/// </summary>
public sealed record RatingDto
{
    public Guid RouteId { get; init; }

    /// <summary>Average rating (1-5) across all users, or 0 if no ratings.</summary>
    public double AverageRating { get; init; }

    /// <summary>Total number of ratings for this route.</summary>
    public int RatingCount { get; init; }

    /// <summary>Current user's rating (1-5), or null if not rated or not logged in.</summary>
    public int? CurrentUserRating { get; init; }
}
