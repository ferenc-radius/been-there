namespace BeenThere.Core.Domain;

/// <summary>
/// Rating (1-5 stars) submitted by a user for a public route (Milestone 6).
/// One rating per (UserId, RouteId) pair; mutable (user can change their rating).
/// </summary>
public sealed class RouteRating
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserId { get; init; } = string.Empty;

    public Guid RouteId { get; init; }

    /// <summary>Rating from 1 to 5 stars.</summary>
    public int Rating { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Route Route { get; init; } = null!;
}
