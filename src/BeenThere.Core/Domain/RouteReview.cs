namespace BeenThere.Core.Domain;

/// <summary>
/// Text review submitted by a user for a public route (Milestone 6).
/// One review per (UserId, RouteId) pair; mutable (user can edit or delete their review).
/// IsFlagged is set by moderation flag submissions.
/// </summary>
public sealed class RouteReview
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserId { get; init; } = string.Empty;

    public Guid RouteId { get; init; }

    /// <summary>Review text; max 500 characters. Can be empty (soft-delete state).</summary>
    public string ReviewText { get; set; } = string.Empty;

    /// <summary>If true, review has been flagged for moderation.</summary>
    public bool IsFlagged { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Route Route { get; init; } = null!;
}
