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

        var userRole = GetUserRole();
        if (string.IsNullOrEmpty(userRole))
        {
            await Clients.Caller.SendAsync("Error", "Unable to determine user role");
            return;
        }

        // Verify user has access to this order
        var hasAccess = await _chatService.VerifyOrderAccessAsync(orderId, userId.Value, userRole);
        if (!hasAccess)
        {
            await Clients.Caller.SendAsync("Error", "You don't have access to this order's chat");
            return;
        }

        // Add user to the order chat group
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

        var userRole = GetUserRole();
        if (string.IsNullOrEmpty(userRole))
        {
            await Clients.Caller.SendAsync("Error", "Unable to determine user role");
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            await Clients.Caller.SendAsync("Error", "Message cannot be empty");
            return;
        }

        // Verify user has access to this order
        var hasAccess = await _chatService.VerifyOrderAccessAsync(orderId, userId.Value, userRole);
        if (!hasAccess)
        {
            await Clients.Caller.SendAsync("Error", "You don't have access to this order's chat");
            return;
        }

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

        var userRole = GetUserRole();
        if (string.IsNullOrEmpty(userRole)) return;

        // Verify access
        var hasAccess = await _chatService.VerifyOrderAccessAsync(orderId, userId.Value, userRole);
        if (!hasAccess) return;

        // Mark messages as read
        await _chatService.MarkMessagesAsReadAsync(orderId, userId.Value);

        // Update last read timestamp
        await _chatService.UpdateLastReadAsync(orderId, userId.Value);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }

    private string GetUserRole()
    {
        return Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }

    private static string GetOrderGroupName(Guid orderId) => $"order_chat_{orderId}";
}
