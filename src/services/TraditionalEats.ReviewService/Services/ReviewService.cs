using Microsoft.EntityFrameworkCore;
using TraditionalEats.ReviewService.Data;
using TraditionalEats.ReviewService.Entities;
using System.Text.Json;

namespace TraditionalEats.ReviewService.Services;

public interface IReviewService
{
    Task<Guid> CreateReviewAsync(Guid orderId, Guid userId, Guid restaurantId, CreateReviewDto dto);
    Task<ReviewDto?> GetReviewAsync(Guid reviewId);
    Task<List<ReviewDto>> GetReviewsByRestaurantAsync(Guid restaurantId, int skip = 0, int take = 20);
    Task<List<ReviewDto>> GetReviewsByUserAsync(Guid userId, int skip = 0, int take = 20);
    Task<bool> UpdateReviewAsync(Guid reviewId, Guid userId, UpdateReviewDto dto);
    Task<bool> AddRestaurantResponseAsync(Guid reviewId, Guid restaurantOwnerId, string response);
    Task<RestaurantRatingDto> GetRestaurantRatingAsync(Guid restaurantId);
}

public class ReviewService : IReviewService
{
    private readonly ReviewDbContext _context;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(ReviewDbContext context, ILogger<ReviewService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Guid> CreateReviewAsync(Guid orderId, Guid userId, Guid restaurantId, CreateReviewDto dto)
    {
        // Check if review already exists for this order
        var existingReview = await _context.Reviews
            .FirstOrDefaultAsync(r => r.OrderId == orderId);

        if (existingReview != null)
        {
            throw new InvalidOperationException("Review already exists for this order");
        }

        var reviewId = Guid.NewGuid();

        var review = new Review
        {
            ReviewId = reviewId,
            OrderId = orderId,
            UserId = userId,
            RestaurantId = restaurantId,
            Rating = dto.Rating,
            Comment = dto.Comment,
            TagsJson = dto.Tags != null ? JsonSerializer.Serialize(dto.Tags) : "[]",
            IsVerified = true, // Verified because it's linked to an order
            IsVisible = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        // Update restaurant rating (would typically be done via event)
        await UpdateRestaurantRatingAsync(restaurantId);

        _logger.LogInformation("Created review {ReviewId} for restaurant {RestaurantId}", reviewId, restaurantId);
        return reviewId;
    }

    public async Task<ReviewDto?> GetReviewAsync(Guid reviewId)
    {
        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.ReviewId == reviewId && r.IsVisible);

        return review == null ? null : MapToDto(review);
    }

    public async Task<List<ReviewDto>> GetReviewsByRestaurantAsync(Guid restaurantId, int skip = 0, int take = 20)
    {
        var reviews = await _context.Reviews
            .Where(r => r.RestaurantId == restaurantId && r.IsVisible)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return reviews.Select(MapToDto).ToList();
    }

    public async Task<List<ReviewDto>> GetReviewsByUserAsync(Guid userId, int skip = 0, int take = 20)
    {
        var reviews = await _context.Reviews
            .Where(r => r.UserId == userId && r.IsVisible)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return reviews.Select(MapToDto).ToList();
    }

    public async Task<bool> UpdateReviewAsync(Guid reviewId, Guid userId, UpdateReviewDto dto)
    {
        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.ReviewId == reviewId && r.UserId == userId);

        if (review == null)
        {
            return false;
        }

        if (dto.Rating.HasValue) review.Rating = dto.Rating.Value;
        if (dto.Comment != null) review.Comment = dto.Comment;
        if (dto.Tags != null) review.TagsJson = JsonSerializer.Serialize(dto.Tags);

        review.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Update restaurant rating
        await UpdateRestaurantRatingAsync(review.RestaurantId);

        return true;
    }

    public async Task<bool> AddRestaurantResponseAsync(Guid reviewId, Guid restaurantOwnerId, string response)
    {
        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.ReviewId == reviewId);

        if (review == null)
        {
            return false;
        }

        // TODO: Verify restaurantOwnerId owns the restaurant
        review.Response = response;
        review.ResponseAt = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<RestaurantRatingDto> GetRestaurantRatingAsync(Guid restaurantId)
    {
        var reviews = await _context.Reviews
            .Where(r => r.RestaurantId == restaurantId && r.IsVisible)
            .ToListAsync();

        if (!reviews.Any())
        {
            return new RestaurantRatingDto
            {
                RestaurantId = restaurantId,
                AverageRating = 0,
                TotalReviews = 0,
                RatingDistribution = new Dictionary<int, int>()
            };
        }

        var averageRating = reviews.Average(r => r.Rating);
        var ratingDistribution = reviews
            .GroupBy(r => r.Rating)
            .ToDictionary(g => g.Key, g => g.Count());

        return new RestaurantRatingDto
        {
            RestaurantId = restaurantId,
            AverageRating = (decimal)averageRating,
            TotalReviews = reviews.Count,
            RatingDistribution = ratingDistribution
        };
    }

    private async Task UpdateRestaurantRatingAsync(Guid restaurantId)
    {
        var rating = await GetRestaurantRatingAsync(restaurantId);
        // TODO: Publish event to update RestaurantService
        _logger.LogInformation("Restaurant {RestaurantId} rating updated: {Rating} ({Count} reviews)", 
            restaurantId, rating.AverageRating, rating.TotalReviews);
    }

    private ReviewDto MapToDto(Review review)
    {
        return new ReviewDto
        {
            ReviewId = review.ReviewId,
            OrderId = review.OrderId,
            UserId = review.UserId,
            RestaurantId = review.RestaurantId,
            Rating = review.Rating,
            Comment = review.Comment,
            Tags = !string.IsNullOrEmpty(review.TagsJson) && review.TagsJson != "[]" 
                ? JsonSerializer.Deserialize<List<string>>(review.TagsJson) ?? new()
                : new(),
            Response = review.Response,
            ResponseAt = review.ResponseAt,
            IsVerified = review.IsVerified,
            IsVisible = review.IsVisible,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt
        };
    }
}

// DTOs
public record CreateReviewDto(int Rating, string? Comment, List<string>? Tags);
public record UpdateReviewDto(int? Rating, string? Comment, List<string>? Tags);

public record ReviewDto
{
    public Guid ReviewId { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public Guid RestaurantId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Response { get; set; }
    public DateTime? ResponseAt { get; set; }
    public bool IsVerified { get; set; }
    public bool IsVisible { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public record RestaurantRatingDto
{
    public Guid RestaurantId { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new();
}
