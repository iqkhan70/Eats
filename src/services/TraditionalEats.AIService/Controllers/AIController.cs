using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionalEats.AIService.Services;

namespace TraditionalEats.AIService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AIController : ControllerBase
{
    private readonly IAIService _aiService;
    private readonly ILogger<AIController> _logger;

    public AIController(IAIService aiService, ILogger<AIController> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    [HttpPost("recommendations")]
    [Authorize]
    public async Task<IActionResult> GetRecommendation([FromBody] GetRecommendationRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var recommendation = await _aiService.GetRecommendationAsync(userId, request.Context);
            return Ok(new { recommendation });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recommendation");
            return StatusCode(500, new { message = "Failed to get recommendation" });
        }
    }

    [HttpPost("ask")]
    public async Task<IActionResult> AnswerQuestion([FromBody] AskQuestionRequest request)
    {
        try
        {
            var answer = await _aiService.AnswerQuestionAsync(request.Question, request.Context);
            return Ok(new { answer });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to answer question");
            return StatusCode(500, new { message = "Failed to answer question" });
        }
    }

    [HttpPost("generate-description")]
    [Authorize(Roles = "Admin,RestaurantOwner")]
    public async Task<IActionResult> GenerateDescription([FromBody] GenerateDescriptionRequest request)
    {
        try
        {
            var description = await _aiService.GenerateDescriptionAsync(request.ItemName, request.Category);
            return Ok(new { description });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate description");
            return StatusCode(500, new { message = "Failed to generate description" });
        }
    }

    [HttpPost("fraud-check")]
    [Authorize(Roles = "Admin,OrderService")]
    public async Task<IActionResult> CheckFraud([FromBody] FraudCheckRequest request)
    {
        try
        {
            var isSuspicious = await _aiService.DetectFraudAsync(
                request.OrderId, 
                request.Amount, 
                request.Metadata);
            return Ok(new { isSuspicious });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check fraud");
            return StatusCode(500, new { message = "Failed to check fraud" });
        }
    }
}

public record GetRecommendationRequest(string Context);
public record AskQuestionRequest(string Question, string? Context = null);
public record GenerateDescriptionRequest(string ItemName, string? Category = null);
public record FraudCheckRequest(Guid OrderId, decimal Amount, Dictionary<string, object>? Metadata = null);
