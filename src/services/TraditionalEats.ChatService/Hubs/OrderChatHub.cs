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
        var userRoles = GetUserRoles();
        var bearerToken = GetBearerToken();
        
        _logger.LogInformation("OnConnectedAsync: UserId={UserId}, Roles={Roles}, BearerTokenPresent={HasToken}, ConnectionId={ConnectionId}", 
            userId, userRoles != null ? string.Join(",", userRoles) : "none", !string.IsNullOrWhiteSpace(bearerToken), Context.ConnectionId);
        
        if (userId == null)
        {
            _logger.LogWarning("OnConnectedAsync: Aborting connection - UserId is null. ConnectionId={ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        _logger.LogInformation("User {UserId} connected to chat hub successfully", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId != null)
        {
            _logger.LogInformation("User {UserId} disconnected from chat hub. Exception: {Exception}, ConnectionId={ConnectionId}", 
                userId, exception?.Message ?? "None", Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning("OnDisconnectedAsync: User disconnected but UserId is null. Exception: {Exception}, ConnectionId={ConnectionId}", 
                exception?.Message ?? "None", Context.ConnectionId);
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

        // Extract bearer token from SignalR connection (for forwarding to OrderService/RestaurantService)
        var bearerToken = GetBearerToken();
        _logger.LogInformation("JoinOrderChat: OrderId={OrderId}, UserId={UserId}, Roles={Roles}, BearerTokenPresent={HasToken}",
            orderId, userId, string.Join(",", userRoles), !string.IsNullOrWhiteSpace(bearerToken));

        // Verify user has access to this order (any role: Customer, Vendor, or Admin)
        var hasAccess = await _chatService.VerifyOrderAccessAsync(orderId, userId.Value, userRoles, bearerToken);
        if (!hasAccess)
        {
            _logger.LogWarning("JoinOrderChat: User {UserId} with roles {Roles} denied access to order {OrderId} chat. Check logs above for details.",
                userId, string.Join(",", userRoles), orderId);
            await Clients.Caller.SendAsync("Error", "You don't have access to this order's chat");
            return;
        }

        _logger.LogInformation("JoinOrderChat: User {UserId} granted access to order {OrderId} chat", userId, orderId);

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
    public async Task SendMessage(Guid orderId, string message, string? metadataJson = null)
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

        // Extract bearer token from SignalR connection
        var bearerToken = GetBearerToken();

        // Verify user has access to this order (any role: Customer, Vendor, or Admin)
        var hasAccess = await _chatService.VerifyOrderAccessAsync(orderId, userId.Value, userRoles, bearerToken);
        if (!hasAccess)
        {
            _logger.LogWarning("User {UserId} denied access to send message to order {OrderId} chat", userId, orderId);
            await Clients.Caller.SendAsync("Error", "You don't have access to this order's chat");
            return;
        }

        var userRole = userRoles.First();
        var senderDisplayName = GetSenderDisplayName();

        // Save message to database
        var chatMessage = await _chatService.SaveMessageAsync(orderId, userId.Value, userRole, senderDisplayName, message, metadataJson);

        // Broadcast to all participants in the order chat group
        var groupName = GetOrderGroupName(orderId);
        await Clients.Group(groupName).SendAsync("ReceiveMessage", new
        {
            MessageId = chatMessage.MessageId,
            OrderId = chatMessage.OrderId,
            SenderId = chatMessage.SenderId,
            SenderRole = chatMessage.SenderRole,
            SenderDisplayName = chatMessage.SenderDisplayName,
            Message = chatMessage.Message,
            SentAt = chatMessage.SentAt,
            MetadataJson = chatMessage.MetadataJson
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

        // Extract bearer token from SignalR connection
        var bearerToken = GetBearerToken();

        // Verify access (any role: Customer, Vendor, or Admin)
        var hasAccess = await _chatService.VerifyOrderAccessAsync(orderId, userId.Value, userRoles, bearerToken);
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

    private string? GetSenderDisplayName()
    {
        if (Context.User == null) return null;
        var name = Context.User.FindFirst(ClaimTypes.Name)?.Value
            ?? Context.User.FindFirst("name")?.Value;
        var email = Context.User.FindFirst(ClaimTypes.Email)?.Value
            ?? Context.User.FindFirst("email")?.Value
            ?? Context.User.FindFirst("preferred_username")?.Value
            ?? Context.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
        if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
        if (!string.IsNullOrWhiteSpace(email)) return email.Trim();
        return null;
    }

    private static string GetOrderGroupName(Guid orderId) => $"order_chat_{orderId}";

    private string? GetBearerToken()
    {
        // Try to get token from query string (SignalR connection)
        var accessToken = Context.GetHttpContext()?.Request.Query["access_token"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            return accessToken;
        }

        // Try to get from Authorization header
        var authHeader = Context.GetHttpContext()?.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }

        return null;
    }
}
