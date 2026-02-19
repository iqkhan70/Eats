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
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "User ID claim is missing" });
            }

            var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            userRoles.AddRange(User.FindAll("role").Select(c => c.Value).Where(v => !userRoles.Contains(v, StringComparer.OrdinalIgnoreCase)));
            if (userRoles.Count == 0)
            {
                return Unauthorized(new { message = "User role claim is missing" });
            }

            var messages = await _chatService.GetOrderMessagesAsync(orderId, userId, userRoles);
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

    // ----------------------------
    // Generic vendor/customer chat
    // ----------------------------

    [HttpPost("vendor/conversations")]
    public async Task<IActionResult> CreateOrGetVendorConversation([FromBody] CreateVendorConversationRequest request)
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "User ID claim is missing" });

            if (request.RestaurantId == Guid.Empty)
                return BadRequest(new { message = "RestaurantId is required" });

            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            roles.AddRange(User.FindAll("role").Select(c => c.Value).Where(v => !roles.Contains(v, StringComparer.OrdinalIgnoreCase)));
            if (roles.Any(r => string.Equals(r, "Vendor", StringComparison.OrdinalIgnoreCase)) ||
                roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)))
            {
                // Vendors/Admins should not create conversations via the customer endpoint.
                // Vendors use the inbox (/vendor/inbox) to respond to customers.
                return Forbid();
            }

            var displayName = User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("name")
                ?? User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue("email");

            var convo = await _chatService.GetOrCreateVendorConversationAsync(request.RestaurantId, userId, displayName);
            return Ok(new
            {
                conversationId = convo.ConversationId,
                restaurantId = convo.RestaurantId,
                customerId = convo.CustomerId,
                customerDisplayName = convo.CustomerDisplayName,
                lastMessageAt = convo.LastMessageAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vendor conversation");
            return StatusCode(500, new { message = "Failed to create conversation" });
        }
    }

    [HttpGet("vendor/conversations/mine")]
    public async Task<IActionResult> GetMyVendorConversations()
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "User ID claim is missing" });

            var list = await _chatService.GetCustomerVendorConversationsAsync(userId);
            return Ok(list.Select(c => new
            {
                conversationId = c.ConversationId,
                restaurantId = c.RestaurantId,
                customerId = c.CustomerId,
                customerDisplayName = c.CustomerDisplayName,
                lastMessageAt = c.LastMessageAt,
                updatedAt = c.UpdatedAt
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer vendor conversations");
            return StatusCode(500, new { message = "Failed to get conversations" });
        }
    }

    [HttpGet("vendor/inbox")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetVendorInbox([FromQuery] int take = 100)
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "User ID claim is missing" });

            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            roles.AddRange(User.FindAll("role").Select(c => c.Value).Where(v => !roles.Contains(v, StringComparer.OrdinalIgnoreCase)));

            if (roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)))
            {
                var adminList = await _chatService.GetAdminVendorInboxAsync(take);
                return Ok(adminList.Select(c => new
                {
                    conversationId = c.ConversationId,
                    restaurantId = c.RestaurantId,
                    customerId = c.CustomerId,
                    customerDisplayName = c.CustomerDisplayName,
                    lastMessageAt = c.LastMessageAt,
                    updatedAt = c.UpdatedAt
                }));
            }

            // Vendor inbox needs RestaurantService call; pass through bearer token from this request
            var auth = Request.Headers["Authorization"].FirstOrDefault();
            var token = (!string.IsNullOrWhiteSpace(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                ? auth.Substring("Bearer ".Length).Trim()
                : null;

            var list = await _chatService.GetVendorInboxAsync(userId, token);
            return Ok(list.Select(c => new
            {
                conversationId = c.ConversationId,
                restaurantId = c.RestaurantId,
                customerId = c.CustomerId,
                customerDisplayName = c.CustomerDisplayName,
                lastMessageAt = c.LastMessageAt,
                updatedAt = c.UpdatedAt
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vendor inbox");
            return StatusCode(500, new { message = "Failed to get vendor inbox" });
        }
    }

    [HttpGet("vendor/conversations/{conversationId}/messages")]
    public async Task<IActionResult> GetVendorConversationMessages(Guid conversationId)
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "User ID claim is missing" });

            var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            userRoles.AddRange(User.FindAll("role").Select(c => c.Value).Where(v => !userRoles.Contains(v, StringComparer.OrdinalIgnoreCase)));
            if (userRoles.Count == 0)
                return Unauthorized(new { message = "User role claim is missing" });

            var auth = Request.Headers["Authorization"].FirstOrDefault();
            var token = (!string.IsNullOrWhiteSpace(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                ? auth.Substring("Bearer ".Length).Trim()
                : null;

            var messages = await _chatService.GetVendorMessagesAsync(conversationId, userId, userRoles, token);
            if (messages.Count == 0)
            {
                // Could be empty conversation OR unauthorized. We can double-check access to return 403.
                var hasAccess = await _chatService.VerifyVendorConversationAccessAsync(conversationId, userId, userRoles, token);
                if (!hasAccess)
                    return Forbid();
            }
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for vendor conversation {ConversationId}", conversationId);
            return StatusCode(500, new { message = "Failed to get messages" });
        }
    }
}

public record CreateVendorConversationRequest(Guid RestaurantId);
