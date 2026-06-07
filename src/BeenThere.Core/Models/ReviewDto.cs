namespace BeenThere.Core.Models;

/// <summary>
/// Public DTO for route reviews (contract safe per ADR-0004 & Milestone 6 decision Q10).
/// Never exposes internal user IDs; only display names.
/// </summary>
public sealed record ReviewDto
{
    public Guid ReviewId { get; init; }

    /// <summary>Display name of the reviewer (from AspNetUser).</summary>
    public string ReviewerName { get; init; } = string.Empty;

    /// <summary>Review text (max 500 chars); may be empty (soft-delete state).</summary>
    public string ReviewText { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
