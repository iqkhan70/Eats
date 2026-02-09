using Microsoft.EntityFrameworkCore;
using TraditionalEats.ReviewService.Data;
using TraditionalEats.ReviewService.Entities;
using System.Text.Json;
using System.Net.Http.Json;

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
    private readonly IHttpClientFactory _httpClientFactory;

    public ReviewService(ReviewDbContext context, ILogger<ReviewService> logger, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Guid> CreateReviewAsync(Guid orderId, Guid userId, Guid restaurantId, CreateReviewDto dto)
    {
        try
        {
            // Validate rating
            if (dto.Rating < 1 || dto.Rating > 5)
            {
                throw new ArgumentException("Rating must be between 1 and 5");
            }

            // Check if review already exists for this order
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.OrderId == orderId);

            if (existingReview != null)
            {
                throw new InvalidOperationException("Review already exists for this order");
            }

            var reviewId = Guid.NewGuid();

            // Serialize tags safely
            string tagsJson = "[]";
            try
            {
                tagsJson = dto.Tags != null && dto.Tags.Any() 
                    ? JsonSerializer.Serialize(dto.Tags) 
                    : "[]";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to serialize tags, using empty array");
                tagsJson = "[]";
            }

            var review = new Review
            {
                ReviewId = reviewId,
                OrderId = orderId,
                UserId = userId,
                RestaurantId = restaurantId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                TagsJson = tagsJson,
                IsVerified = true, // Verified because it's linked to an order
                IsVisible = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException conEx)
            {
                _logger.LogError(conEx, "Concurrency error when saving review");
                throw new InvalidOperationException("Review was modified by another process. Please try again.", conEx);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                var innerMessage = dbEx.InnerException?.Message ?? "Unknown database error";
                _logger.LogError(dbEx, "Database error when saving review. InnerException: {InnerException}, StackTrace: {StackTrace}", 
                    innerMessage, dbEx.InnerException?.StackTrace);
                
                // Check for specific database errors
                if (innerMessage.Contains("Duplicate entry") || innerMessage.Contains("UNIQUE constraint"))
                {
                    throw new InvalidOperationException("A review already exists for this order.", dbEx);
                }
                else if (innerMessage.Contains("Cannot connect") || innerMessage.Contains("Access denied"))
                {
                    throw new InvalidOperationException("Database connection failed. Please check ReviewService database configuration.", dbEx);
                }
                else
                {
                    throw new InvalidOperationException($"Database error: {innerMessage}", dbEx);
                }
            }

            // Update restaurant rating (would typically be done via event)
            // Don't fail the review creation if rating update fails
            try
            {
                await UpdateRestaurantRatingAsync(restaurantId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update restaurant rating for {RestaurantId}, but review was created", restaurantId);
            }

            _logger.LogInformation("Created review {ReviewId} for restaurant {RestaurantId}", reviewId, restaurantId);
            return reviewId;
        }
        catch (InvalidOperationException)
        {
            // Re-throw InvalidOperationException as-is (review already exists)
            throw;
        }
        catch (ArgumentException)
        {
            // Re-throw ArgumentException as-is (validation error)
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review for order {OrderId}, user {UserId}, restaurant {RestaurantId}. Error: {Message}", 
                orderId, userId, restaurantId, ex.Message);
            throw;
        }
    }

    public async Task<ReviewDto?> GetReviewAsync(Guid reviewId)
    {
        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.ReviewId == reviewId && r.IsVisible);

        return review == null ? null : MapToDto(review);
    }

    public async Task<List<ReviewDto>> GetReviewsByRestaurantAsync(Guid restaurantId, int skip = 0, int take = 20)
    {
        try
        {
            var reviews = await _context.Reviews
                .Where(r => r.RestaurantId == restaurantId && r.IsVisible)
                .OrderByDescending(r => r.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            var reviewDtos = new List<ReviewDto>();
            foreach (var review in reviews)
            {
                try
                {
                    reviewDtos.Add(MapToDto(review));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error mapping review {ReviewId} to DTO, skipping", review.ReviewId);
                    // Skip this review and continue with others
                }
            }

            return reviewDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reviews for restaurant {RestaurantId}. Error: {Message}", 
                restaurantId, ex.Message);
            // Return empty list instead of throwing to prevent UI crashes
            return new List<ReviewDto>();
        }
    }

    public async Task<List<ReviewDto>> GetReviewsByUserAsync(Guid userId, int skip = 0, int take = 20)
    {
        try
        {
            var reviews = await _context.Reviews
                .Where(r => r.UserId == userId && r.IsVisible)
                .OrderByDescending(r => r.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            var reviewDtos = new List<ReviewDto>();
            foreach (var review in reviews)
            {
                try
                {
                    reviewDtos.Add(MapToDto(review));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error mapping review {ReviewId} to DTO, skipping", review.ReviewId);
                    // Skip this review and continue with others
                }
            }

            return reviewDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reviews for user {UserId}. Error: {Message}", 
                userId, ex.Message);
            // Return empty list instead of throwing to prevent UI crashes
            return new List<ReviewDto>();
        }
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
        try
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

            // Filter out invalid ratings (should be 1-5)
            var validReviews = reviews.Where(r => r.Rating >= 1 && r.Rating <= 5).ToList();
            
            if (!validReviews.Any())
            {
                return new RestaurantRatingDto
                {
                    RestaurantId = restaurantId,
                    AverageRating = 0,
                    TotalReviews = 0,
                    RatingDistribution = new Dictionary<int, int>()
                };
            }

            var averageRating = validReviews.Average(r => r.Rating);
            var ratingDistribution = validReviews
                .GroupBy(r => r.Rating)
                .ToDictionary(g => g.Key, g => g.Count());

            return new RestaurantRatingDto
            {
                RestaurantId = restaurantId,
                AverageRating = (decimal)averageRating,
                TotalReviews = validReviews.Count,
                RatingDistribution = ratingDistribution
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting restaurant rating for {RestaurantId}", restaurantId);
            // Return default rating instead of throwing
            return new RestaurantRatingDto
            {
                RestaurantId = restaurantId,
                AverageRating = 0,
                TotalReviews = 0,
                RatingDistribution = new Dictionary<int, int>()
            };
        }
    }

    private async Task UpdateRestaurantRatingAsync(Guid restaurantId)
    {
        var rating = await GetRestaurantRatingAsync(restaurantId);
        _logger.LogInformation("Restaurant {RestaurantId} rating updated: {Rating} ({Count} reviews)", 
            restaurantId, rating.AverageRating, rating.TotalReviews);
        
        // Calculate and update Elo rating
        try
        {
            // Get all reviews for this restaurant to recalculate Elo
            var reviews = await _context.Reviews
                .Where(r => r.RestaurantId == restaurantId && r.IsVisible)
                .OrderBy(r => r.CreatedAt)
                .Select(r => r.Rating)
                .ToListAsync();

            // Recalculate Elo from scratch based on all reviews (starting from base)
            // This ensures consistency even if RestaurantService Elo gets out of sync
            decimal newElo = EloRatingService.RecalculateElo(reviews, EloRatingService.GetBaseEloRating());

            // Update Elo rating in RestaurantService
            var httpClient = _httpClientFactory.CreateClient("RestaurantService");
            var updateRequest = new { EloRating = newElo };
            var updateResponse = await httpClient.PostAsJsonAsync($"/api/Restaurant/{restaurantId}/elo-rating", updateRequest);
            
            if (updateResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("Updated Elo rating for restaurant {RestaurantId} to {EloRating}", restaurantId, newElo);
            }
            else
            {
                var errorContent = await updateResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to update Elo rating for restaurant {RestaurantId}. Status: {Status}, Response: {Response}", 
                    restaurantId, updateResponse.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update Elo rating for restaurant {RestaurantId}, but review was created", restaurantId);
        }
    }

    private ReviewDto MapToDto(Review review)
    {
        try
        {
            List<string> tags = new();
            if (!string.IsNullOrEmpty(review.TagsJson) && review.TagsJson != "[]")
            {
                try
                {
                    tags = JsonSerializer.Deserialize<List<string>>(review.TagsJson) ?? new();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize TagsJson for review {ReviewId}: {TagsJson}", 
                        review.ReviewId, review.TagsJson);
                    tags = new();
                }
            }

            return new ReviewDto
            {
                ReviewId = review.ReviewId,
                OrderId = review.OrderId,
                UserId = review.UserId,
                RestaurantId = review.RestaurantId,
                Rating = review.Rating,
                Comment = review.Comment,
                Tags = tags,
                Response = review.Response,
                ResponseAt = review.ResponseAt,
                IsVerified = review.IsVerified,
                IsVisible = review.IsVisible,
                CreatedAt = review.CreatedAt,
                UpdatedAt = review.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping review {ReviewId} to DTO", review.ReviewId);
            throw;
        }
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
