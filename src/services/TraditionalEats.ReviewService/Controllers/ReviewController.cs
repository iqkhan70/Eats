using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionalEats.ReviewService.Services;

namespace TraditionalEats.ReviewService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(IReviewService reviewService, ILogger<ReviewController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var reviewId = await _reviewService.CreateReviewAsync(
                request.OrderId, 
                userId, 
                request.RestaurantId, 
                request.Review);
            return Ok(new { reviewId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create review");
            return StatusCode(500, new { message = "Failed to create review" });
        }
    }

    [HttpGet("{reviewId}")]
    public async Task<IActionResult> GetReview(Guid reviewId)
    {
        try
        {
            var review = await _reviewService.GetReviewAsync(reviewId);
            if (review == null)
            {
                return NotFound(new { message = "Review not found" });
            }
            return Ok(review);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get review");
            return StatusCode(500, new { message = "Failed to get review" });
        }
    }

    [HttpGet("restaurant/{restaurantId}")]
    public async Task<IActionResult> GetReviewsByRestaurant(
        Guid restaurantId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        try
        {
            var reviews = await _reviewService.GetReviewsByRestaurantAsync(restaurantId, skip, take);
            return Ok(reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get reviews");
            return StatusCode(500, new { message = "Failed to get reviews" });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMyReviews(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var reviews = await _reviewService.GetReviewsByUserAsync(userId, skip, take);
            return Ok(reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get reviews");
            return StatusCode(500, new { message = "Failed to get reviews" });
        }
    }

    [HttpPut("{reviewId}")]
    [Authorize]
    public async Task<IActionResult> UpdateReview(Guid reviewId, [FromBody] UpdateReviewDto dto)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var success = await _reviewService.UpdateReviewAsync(reviewId, userId, dto);
            if (!success)
            {
                return NotFound(new { message = "Review not found or you don't have permission" });
            }
            return Ok(new { message = "Review updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update review");
            return StatusCode(500, new { message = "Failed to update review" });
        }
    }

    [HttpPost("{reviewId}/response")]
    [Authorize(Roles = "RestaurantOwner")]
    public async Task<IActionResult> AddRestaurantResponse(Guid reviewId, [FromBody] AddResponseRequest request)
    {
        try
        {
            var restaurantOwnerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var success = await _reviewService.AddRestaurantResponseAsync(reviewId, restaurantOwnerId, request.Response);
            if (!success)
            {
                return NotFound(new { message = "Review not found" });
            }
            return Ok(new { message = "Response added successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add restaurant response");
            return StatusCode(500, new { message = "Failed to add restaurant response" });
        }
    }

    [HttpGet("restaurant/{restaurantId}/rating")]
    public async Task<IActionResult> GetRestaurantRating(Guid restaurantId)
    {
        try
        {
            var rating = await _reviewService.GetRestaurantRatingAsync(restaurantId);
            return Ok(rating);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get restaurant rating");
            return StatusCode(500, new { message = "Failed to get restaurant rating" });
        }
    }
}

public record CreateReviewRequest(Guid OrderId, Guid RestaurantId, CreateReviewDto Review);
public record AddResponseRequest(string Response);
