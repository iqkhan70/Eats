using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionalEats.ChatService.Services;

namespace TraditionalEats.ChatService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpGet("orders/{orderId}/messages")]
    public async Task<IActionResult> GetOrderMessages(Guid orderId)
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "User ID claim is missing" });
            }

            var messages = await _chatService.GetOrderMessagesAsync(orderId, userId, userRole);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for order {OrderId}", orderId);
            return StatusCode(500, new { message = "Failed to get messages" });
        }
    }

    [HttpGet("orders/{orderId}/unread-count")]
    public async Task<IActionResult> GetUnreadCount(Guid orderId)
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "User ID claim is missing" });
            }

            var count = await _chatService.GetUnreadMessageCountAsync(orderId, userId);
            return Ok(new { orderId, unreadCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for order {OrderId}", orderId);
            return StatusCode(500, new { message = "Failed to get unread count" });
        }
    }
}
