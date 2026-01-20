using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionEats.PromotionService.Services;

namespace TraditionEats.PromotionService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PromotionController : ControllerBase
{
    private readonly IPromotionService _promotionService;
    private readonly ILogger<PromotionController> _logger;

    public PromotionController(IPromotionService promotionService, ILogger<PromotionController> logger)
    {
        _promotionService = promotionService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Roles = "Admin,RestaurantOwner")]
    public async Task<IActionResult> CreatePromotion([FromBody] CreatePromotionDto dto)
    {
        try
        {
            var promotionId = await _promotionService.CreatePromotionAsync(dto);
            return Ok(new { promotionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create promotion");
            return StatusCode(500, new { message = "Failed to create promotion" });
        }
    }

    [HttpGet("{promotionId}")]
    public async Task<IActionResult> GetPromotion(Guid promotionId)
    {
        try
        {
            var promotion = await _promotionService.GetPromotionAsync(promotionId);
            if (promotion == null)
            {
                return NotFound(new { message = "Promotion not found" });
            }
            return Ok(promotion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get promotion");
            return StatusCode(500, new { message = "Failed to get promotion" });
        }
    }

    [HttpGet("code/{code}")]
    public async Task<IActionResult> GetPromotionByCode(string code)
    {
        try
        {
            var promotion = await _promotionService.GetPromotionByCodeAsync(code);
            if (promotion == null)
            {
                return NotFound(new { message = "Promotion not found" });
            }
            return Ok(promotion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get promotion");
            return StatusCode(500, new { message = "Failed to get promotion" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetPromotions(
        [FromQuery] Guid? restaurantId = null,
        [FromQuery] bool activeOnly = true)
    {
        try
        {
            var promotions = await _promotionService.GetPromotionsAsync(restaurantId, activeOnly);
            return Ok(promotions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get promotions");
            return StatusCode(500, new { message = "Failed to get promotions" });
        }
    }

    [HttpPost("validate")]
    [Authorize]
    public async Task<IActionResult> ValidatePromotion([FromBody] ValidatePromotionRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isValid = await _promotionService.ValidatePromotionAsync(request.Code, userId, request.OrderAmount);
            return Ok(new { isValid });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate promotion");
            return StatusCode(500, new { message = "Failed to validate promotion" });
        }
    }

    [HttpPost("apply")]
    [Authorize]
    public async Task<IActionResult> ApplyPromotion([FromBody] ApplyPromotionRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var usageId = await _promotionService.ApplyPromotionAsync(
                request.Code, 
                userId, 
                request.OrderId, 
                request.OrderAmount);
            return Ok(new { usageId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply promotion");
            return StatusCode(500, new { message = "Failed to apply promotion" });
        }
    }

    [HttpPost("{promotionId}/calculate")]
    public async Task<IActionResult> CalculateDiscount(Guid promotionId, [FromBody] CalculateDiscountRequest request)
    {
        try
        {
            var discount = await _promotionService.CalculateDiscountAsync(promotionId, request.OrderAmount);
            return Ok(new { discount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate discount");
            return StatusCode(500, new { message = "Failed to calculate discount" });
        }
    }
}

public record ValidatePromotionRequest(string Code, decimal OrderAmount);
public record ApplyPromotionRequest(string Code, Guid OrderId, decimal OrderAmount);
public record CalculateDiscountRequest(decimal OrderAmount);
