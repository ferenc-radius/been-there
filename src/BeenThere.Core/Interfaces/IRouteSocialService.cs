using BeenThere.Core.Domain;
using BeenThere.Core.Models;

namespace BeenThere.Core.Interfaces;

/// <summary>
/// Service for social features: ratings, reviews, and intersection analysis (Milestone 6).
/// All operations are scoped to public routes and the current user's own routes.
/// </summary>
public interface IRouteSocialService
{
    /// <summary>
    /// Get aggregated ratings for a route (average, count) and current user's own rating if present.
    /// </summary>
    Task<RatingDto> GetRatingsForRouteAsync(Guid routeId, CancellationToken ct = default);

    /// <summary>
    /// Get all reviews for a route (paginated, unflagged by default).
    /// Limited to 50 results max per design (Milestone 6 Q6).
    /// </summary>
    Task<List<ReviewDto>> GetReviewsForRouteAsync(Guid routeId, int maxResults = 50, CancellationToken ct = default);

    /// <summary>
    /// Submit or update a rating for a route (1-5 stars). One rating per (user, route).
    /// Only allowed on public routes or routes owned by the current user.
    /// </summary>
    Task<RatingDto> SubmitRatingAsync(Guid routeId, int rating, CancellationToken ct = default);

    /// <summary>
    /// Submit or update a review for a route. One review per (user, route).
    /// Reviewtext max 500 chars. Only allowed on public routes.
    /// </summary>
    Task<ReviewDto> SubmitReviewAsync(Guid routeId, string reviewText, CancellationToken ct = default);

    /// <summary>
    /// Delete a review by marking it as soft-deleted (empty review_text).
    /// Only allowed if the current user owns the review.
    /// </summary>
    Task DeleteReviewAsync(Guid reviewId, CancellationToken ct = default);

    /// <summary>
    /// Flag a review for moderation (sets is_flagged = true).
    /// </summary>
    Task FlagReviewAsync(Guid reviewId, string? reason = null, CancellationToken ct = default);

    /// <summary>
    /// Get routes that overlap with the given route, showing which other users have overlapped it.
    /// Results limited to 50 users max (Milestone 6 Q6).
    /// Uses ST_Intersection / ST_Length with 5-min memory cache per (routeId, currentUserId).
    /// </summary>
    Task<List<IntersectionDto>> GetIntersectionsAsync(Guid routeId, CancellationToken ct = default);

    /// <summary>
    /// Count of total overlapping routes (for badge display in UI).
    /// </summary>
    Task<int> CountIntersectionsAsync(Guid routeId, CancellationToken ct = default);

    /// <summary>
    /// Bulk-count intersections for many routes at once. Optimized for list views.
    /// Returns a dictionary keyed by route id with the number of OTHER users' routes that overlap.
    /// </summary>
    Task<Dictionary<Guid, int>> CountIntersectionsForRoutesAsync(IEnumerable<Guid> routeIds, CancellationToken ct = default);
}
