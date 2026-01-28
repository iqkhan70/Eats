using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using TraditionalEats.ChatService.Services;

namespace TraditionalEats.ChatService.Hubs;

[Authorize]
public class OrderChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly ILogger<OrderChatHub> _logger;

    public OrderChatHub(IChatService chatService, ILogger<OrderChatHub> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            Context.Abort();
            return;
        }

        _logger.LogInformation("User {UserId} connected to chat hub", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId != null)
        {
            _logger.LogInformation("User {UserId} disconnected from chat hub", userId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join the chat room for a specific order
    /// </summary>
    public async Task JoinOrderChat(Guid orderId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var userRoles = GetUserRoles();
        if (userRoles == null || !userRoles.Any())
        {
            await Clients.Caller.SendAsync("Error", "Unable to determine user role");
            return;
        }

        // Verify user has access to this order (any role: Customer, Vendor, or Admin)
        var hasAccess = await _chatService.VerifyOrderAccessAsync(orderId, userId.Value, userRoles);
        if (!hasAccess)
        {
            await Clients.Caller.SendAsync("Error", "You don't have access to this order's chat");
            return;
        }

        // Add user to the order chat group (use first role for participant label)
        var userRole = userRoles.First();
        var groupName = GetOrderGroupName(orderId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        // Ensure participant record exists
        await _chatService.EnsureParticipantAsync(orderId, userId.Value, userRole);

        _logger.LogInformation("User {UserId} ({Role}) joined chat for order {OrderId}", userId, userRole, orderId);

        // Notify others in the group
        await Clients.Group(groupName).SendAsync("UserJoined", new
        {
            UserId = userId,
            Role = userRole,
            OrderId = orderId
        });
    }

    /// <summary>
    /// Leave the chat room for a specific order
    /// </summary>
    public async Task LeaveOrderChat(Guid orderId)
    {
        var userId = GetUserId();
        if (userId == null) return;

        var groupName = GetOrderGroupName(orderId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation("User {UserId} left chat for order {OrderId}", userId, orderId);

        await Clients.Group(groupName).SendAsync("UserLeft", new
        {
            UserId = userId,
            OrderId = orderId
        });
    }

    /// <summary>
    /// Send a message to the order chat
    /// </summary>
    public async Task SendMessage(Guid orderId, string message)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var userRoles = GetUserRoles();
        if (userRoles == null || !userRoles.Any())
        {
            await Clients.Caller.SendAsync("Error", "Unable to determine user role");
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            await Clients.Caller.SendAsync("Error", "Message cannot be empty");
            return;
        }

        // Verify user has access to this order (any role: Customer, Vendor, or Admin)
        var hasAccess = await _chatService.VerifyOrderAccessAsync(orderId, userId.Value, userRoles);
        if (!hasAccess)
        {
            await Clients.Caller.SendAsync("Error", "You don't have access to this order's chat");
            return;
        }

        var userRole = userRoles.First();

        // Save message to database
        var chatMessage = await _chatService.SaveMessageAsync(orderId, userId.Value, userRole, message);

        // Broadcast to all participants in the order chat group
        var groupName = GetOrderGroupName(orderId);
        await Clients.Group(groupName).SendAsync("ReceiveMessage", new
        {
            MessageId = chatMessage.MessageId,
            OrderId = chatMessage.OrderId,
            SenderId = chatMessage.SenderId,
            SenderRole = chatMessage.SenderRole,
            Message = chatMessage.Message,
            SentAt = chatMessage.SentAt
        });

        _logger.LogInformation("User {UserId} sent message to order {OrderId} chat", userId, orderId);
    }

    /// <summary>
    /// Mark messages as read
    /// </summary>
    public async Task MarkMessagesAsRead(Guid orderId)
    {
        var userId = GetUserId();
        if (userId == null) return;

        var userRoles = GetUserRoles();
        if (userRoles == null || !userRoles.Any()) return;

        // Verify access (any role: Customer, Vendor, or Admin)
        var hasAccess = await _chatService.VerifyOrderAccessAsync(orderId, userId.Value, userRoles);
        if (!hasAccess) return;

        // Mark messages as read
        await _chatService.MarkMessagesAsReadAsync(orderId, userId.Value);

        // Update last read timestamp
        await _chatService.UpdateLastReadAsync(orderId, userId.Value);
    }

    private Guid? GetUserId()
    {
        // Try NameIdentifier first, then "sub" (standard JWT subject claim)
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }

    private string GetUserRole()
    {
        var roles = GetUserRoles();
        return roles?.FirstOrDefault() ?? string.Empty;
    }

    private List<string> GetUserRoles()
    {
        var list = new List<string>();
        if (Context.User == null) return list;
        foreach (var claim in Context.User.FindAll(ClaimTypes.Role))
        {
            if (!string.IsNullOrWhiteSpace(claim.Value)) list.Add(claim.Value.Trim());
        }
        foreach (var claim in Context.User.FindAll("role"))
        {
            if (!string.IsNullOrWhiteSpace(claim.Value) && !list.Contains(claim.Value.Trim(), StringComparer.OrdinalIgnoreCase))
                list.Add(claim.Value.Trim());
        }
        return list;
    }

    private static string GetOrderGroupName(Guid orderId) => $"order_chat_{orderId}";
}
