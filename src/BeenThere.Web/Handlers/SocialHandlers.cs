using BeenThere.Core.Interfaces;
using BeenThere.Core.Models;

namespace BeenThere.Web.Handlers;

/// <summary>
/// Handlers for Milestone 6 social features: ratings, reviews, and intersections.
/// All endpoints require authentication for writes. Reads are allowed for public routes.
/// </summary>
internal static class SocialHandlers
{
    /// <summary>Get aggregated ratings and current user's rating for a route.</summary>
    internal static async Task<IResult> GetRatings(
        Guid routeId,
        IRouteSocialService socialService,
        CancellationToken ct)
    {
        try
        {
            var ratings = await socialService.GetRatingsForRouteAsync(routeId, ct);
            return Results.Ok(ratings);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Submit or update a rating for a route (1-5 stars).</summary>
    internal static async Task<IResult> SubmitRating(
        Guid routeId,
        RatingSubmissionRequest request,
        IRouteSocialService socialService,
        CancellationToken ct)
    {
        try
        {
            var ratings = await socialService.SubmitRatingAsync(routeId, request.Rating, ct);
            return Results.Ok(ratings);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
    }

    /// <summary>Get reviews for a route (unflagged, paginated).</summary>
    internal static async Task<IResult> GetReviews(
        Guid routeId,
        int maxResults,
        IRouteSocialService socialService,
        CancellationToken ct)
    {
        try
        {
            var reviews = await socialService.GetReviewsForRouteAsync(routeId, maxResults, ct);
            return Results.Ok(reviews);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound(new { error = "Route not found." });
        }
    }

    /// <summary>Submit or update a review for a route.</summary>
    internal static async Task<IResult> SubmitReview(
        Guid routeId,
        ReviewSubmissionRequest request,
        IRouteSocialService socialService,
        CancellationToken ct)
    {
        try
        {
            var review = await socialService.SubmitReviewAsync(routeId, request.ReviewText, ct);
            return Results.Ok(review);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
    }

    /// <summary>Delete a review (soft-delete by clearing text).</summary>
    internal static async Task<IResult> DeleteReview(
        Guid reviewId,
        IRouteSocialService socialService,
        CancellationToken ct)
    {
        try
        {
            await socialService.DeleteReviewAsync(reviewId, ct);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
    }

    /// <summary>Flag a review for moderation.</summary>
    internal static async Task<IResult> FlagReview(
        Guid reviewId,
        FlagReviewRequest request,
        IRouteSocialService socialService,
        CancellationToken ct)
    {
        try
        {
            await socialService.FlagReviewAsync(reviewId, request.Reason, ct);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Get "been-there too" intersections for a route (limited to 50 results).</summary>
    internal static async Task<IResult> GetIntersections(
        Guid routeId,
        IRouteSocialService socialService,
        CancellationToken ct)
    {
        try
        {
            var intersections = await socialService.GetIntersectionsAsync(routeId, ct);
            return Results.Ok(intersections);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Get count of "been-there too" intersections for a route (for badge display).</summary>
    internal static async Task<IResult> CountIntersections(
        Guid routeId,
        IRouteSocialService socialService,
        CancellationToken ct)
    {
        try
        {
            var count = await socialService.CountIntersectionsAsync(routeId, ct);
            return Results.Ok(new { count });
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }
}

// Request DTOs (contract safe — no internal IDs exposed)
internal sealed record RatingSubmissionRequest
{
    public int Rating { get; init; }
}

internal sealed record ReviewSubmissionRequest
{
    public string ReviewText { get; init; } = string.Empty;
}

internal sealed record FlagReviewRequest
{
    public string? Reason { get; init; }
}
