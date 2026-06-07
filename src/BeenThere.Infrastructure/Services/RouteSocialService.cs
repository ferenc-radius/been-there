using BeenThere.Core.Domain;
using BeenThere.Core.Interfaces;
using BeenThere.Core.Models;
using BeenThere.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BeenThere.Infrastructure.Services;

/// <summary>
/// Implementation of IRouteSocialService for Milestone 6 social features.
/// Handles ratings, reviews, and intersection analysis with caching.
/// </summary>
internal sealed class RouteSocialService : IRouteSocialService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IMemoryCache _cache;

    private const int MaxIntersectionResults = 50;
    private static readonly TimeSpan IntersectionCacheTtl = TimeSpan.FromMinutes(5);

    public RouteSocialService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        UserManager<IdentityUser> userManager,
        IMemoryCache cache)
    {
        _context = context;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _cache = cache;
    }

    public async Task<RatingDto> GetRatingsForRouteAsync(Guid routeId, CancellationToken ct = default)
    {
        var ratings = await _context.RouteRatings
            .Where(rr => rr.RouteId == routeId)
            .AsNoTracking()
            .ToListAsync(ct);

        var averageRating = ratings.Count > 0
            ? Math.Round(ratings.Average(r => r.Rating), 1)
            : 0.0;

        var currentUserRating = _currentUserService.UserId != null
            ? ratings.FirstOrDefault(r => r.UserId == _currentUserService.UserId)?.Rating
            : null;

        return new RatingDto
        {
            RouteId = routeId,
            AverageRating = averageRating,
            RatingCount = ratings.Count,
            CurrentUserRating = currentUserRating
        };
    }

    public async Task<List<ReviewDto>> GetReviewsForRouteAsync(Guid routeId, int maxResults = 50, CancellationToken ct = default)
    {
        var reviews = await _context.RouteReviews
            .Where(rr => rr.RouteId == routeId && !rr.IsFlagged)
            .AsNoTracking()
            .OrderByDescending(rr => rr.CreatedAt)
            .Take(maxResults)
            .ToListAsync(ct);

        var result = new List<ReviewDto>();
        foreach (var review in reviews)
        {
            var user = await _userManager.FindByIdAsync(review.UserId);
            result.Add(new ReviewDto
            {
                ReviewId = review.Id,
                ReviewerName = user?.UserName ?? "Unknown",
                ReviewText = review.ReviewText,
                CreatedAt = review.CreatedAt,
                UpdatedAt = review.UpdatedAt
            });
        }

        return result;
    }

    public async Task<RatingDto> SubmitRatingAsync(Guid routeId, int rating, CancellationToken ct = default)
    {
        if (rating < 1 || rating > 5)
        {
            throw new ArgumentException("Rating must be between 1 and 5.", nameof(rating));
        }

        var userId = _currentUserService.UserId ?? throw new InvalidOperationException("Current user not found.");

        // Verify route exists (all routes are visible to all users)
        var route = await _context.Routes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == routeId, ct)
            ?? throw new InvalidOperationException($"Route {routeId} not found.");

        // Insert or update rating
        var existingRating = await _context.RouteRatings
            .FirstOrDefaultAsync(rr => rr.RouteId == routeId && rr.UserId == userId, ct);

        if (existingRating != null)
        {
            existingRating.Rating = rating;
            existingRating.UpdatedAt = DateTimeOffset.UtcNow;
            _context.RouteRatings.Update(existingRating);
        }
        else
        {
            var newRating = new RouteRating
            {
                UserId = userId,
                RouteId = routeId,
                Rating = rating
            };
            await _context.RouteRatings.AddAsync(newRating, ct);
        }

        await _context.SaveChangesAsync(ct);

        return await GetRatingsForRouteAsync(routeId, ct);
    }

    public async Task<ReviewDto> SubmitReviewAsync(Guid routeId, string reviewText, CancellationToken ct = default)
    {
        if (reviewText.Length > 500)
        {
            throw new ArgumentException("Review text must not exceed 500 characters.", nameof(reviewText));
        }

        var userId = _currentUserService.UserId ?? throw new InvalidOperationException("Current user not found.");

        // Verify route exists (all routes are visible to all users)
        var route = await _context.Routes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == routeId, ct)
            ?? throw new InvalidOperationException($"Route {routeId} not found.");

        // Insert or update review
        var existingReview = await _context.RouteReviews
            .FirstOrDefaultAsync(rr => rr.RouteId == routeId && rr.UserId == userId, ct);

        if (existingReview != null)
        {
            existingReview.ReviewText = reviewText;
            existingReview.UpdatedAt = DateTimeOffset.UtcNow;
            _context.RouteReviews.Update(existingReview);
        }
        else
        {
            var newReview = new RouteReview
            {
                UserId = userId,
                RouteId = routeId,
                ReviewText = reviewText
            };
            await _context.RouteReviews.AddAsync(newReview, ct);
        }

        await _context.SaveChangesAsync(ct);

        var savedReview = await _context.RouteReviews
            .FirstOrDefaultAsync(rr => rr.RouteId == routeId && rr.UserId == userId, ct)
            ?? throw new InvalidOperationException("Failed to retrieve saved review.");

        var user = await _userManager.FindByIdAsync(userId);
        return new ReviewDto
        {
            ReviewId = savedReview.Id,
            ReviewerName = user?.UserName ?? "Unknown",
            ReviewText = savedReview.ReviewText,
            CreatedAt = savedReview.CreatedAt,
            UpdatedAt = savedReview.UpdatedAt
        };
    }

    public async Task DeleteReviewAsync(Guid reviewId, CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId ?? throw new InvalidOperationException("Current user not found.");

        var review = await _context.RouteReviews
            .FirstOrDefaultAsync(rr => rr.Id == reviewId, ct)
            ?? throw new InvalidOperationException($"Review {reviewId} not found.");

        if (review.UserId != userId)
        {
            throw new UnauthorizedAccessException("You can only delete your own reviews.");
        }

        // Soft delete: clear review text
        review.ReviewText = string.Empty;
        review.UpdatedAt = DateTimeOffset.UtcNow;
        _context.RouteReviews.Update(review);

        await _context.SaveChangesAsync(ct);
    }

    public async Task FlagReviewAsync(Guid reviewId, string? reason = null, CancellationToken ct = default)
    {
        var review = await _context.RouteReviews
            .FirstOrDefaultAsync(rr => rr.Id == reviewId, ct)
            ?? throw new InvalidOperationException($"Review {reviewId} not found.");

        review.IsFlagged = true;
        _context.RouteReviews.Update(review);

        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<IntersectionDto>> GetIntersectionsAsync(Guid routeId, CancellationToken ct = default)
    {
        var cacheKey = $"intersections_{routeId}_{_currentUserService.UserId}";

        if (_cache.TryGetValue(cacheKey, out List<IntersectionDto>? cached))
        {
            return cached ?? [];
        }

        var queryRoute = await _context.Routes
            .Where(r => r.Id == routeId)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Route {routeId} not found or not accessible.");

        if (queryRoute.Geom == null)
        {
            return [];
        }

        // Push ST_Intersects to PostGIS — uses GIST spatial index on geom column
        // Then compute overlap % only for the matching rows, still in the DB
        var intersectingRoutes = await _context.Routes
            .Where(r => r.Id != routeId && r.Geom != null && r.Geom.Intersects(queryRoute.Geom))
            .AsNoTracking()
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.UserId,
                OverlapLength = r.Geom!.Intersection(queryRoute.Geom!).Length,
            })
            .ToListAsync(ct);

        var queryLength = queryRoute.Geom.Length;

        // Batch-load display names for matching users
        var userIds = intersectingRoutes.Select(r => r.UserId).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync(ct);
        var userMap = users.ToDictionary(u => u.Id, u => u.UserName ?? "Unknown");

        var intersections = intersectingRoutes
            .Select(r => new IntersectionDto
            {
                OtherUserName = userMap.GetValueOrDefault(r.UserId, "Unknown"),
                RouteName = r.Name,
                OverlapPercentage = queryLength > 0 ? Math.Round(r.OverlapLength / queryLength * 100, 2) : 0,
                OtherRouteId = r.Id,
            })
            .OrderByDescending(x => x.OverlapPercentage)
            .Take(MaxIntersectionResults)
            .ToList();

        _cache.Set(cacheKey, intersections, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = IntersectionCacheTtl });

        return intersections;
    }

    public async Task<int> CountIntersectionsAsync(Guid routeId, CancellationToken ct = default)
    {
        var intersections = await GetIntersectionsAsync(routeId, ct);
        return intersections.Count;
    }

    public async Task<Dictionary<Guid, int>> CountIntersectionsForRoutesAsync(IEnumerable<Guid> routeIds, CancellationToken ct = default)
    {
        var ids = routeIds.ToList();
        var result = ids.ToDictionary(id => id, _ => 0);

        if (ids.Count == 0) return result;

        // Load the geometries for the routes we care about
        var queryRoutes = await _context.Routes
            .Where(r => ids.Contains(r.Id) && r.Geom != null)
            .AsNoTracking()
            .Select(r => new { r.Id, r.Geom, r.UserId })
            .ToListAsync(ct);

        // For each route, use ST_Intersects to count distinct overlapping users — pushed to PostGIS
        foreach (var query in queryRoutes)
        {
            if (query.Geom == null) continue;

            var geom = query.Geom;
            var ownerId = query.UserId;

            var count = await _context.Routes
                .Where(r => r.Id != query.Id && r.Geom != null && r.UserId != ownerId && r.Geom.Intersects(geom))
                .Select(r => r.UserId)
                .Distinct()
                .CountAsync(ct);

            result[query.Id] = count;
        }

        return result;
    }
}
