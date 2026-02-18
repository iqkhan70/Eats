using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using TraditionalEats.ChatService.Services;

namespace TraditionalEats.ChatService.Hubs;

[Authorize]
public class VendorChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly ILogger<VendorChatHub> _logger;

    public VendorChatHub(IChatService chatService, ILogger<VendorChatHub> logger)
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
        await base.OnConnectedAsync();
    }

    public async Task JoinVendorConversation(Guid conversationId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var roles = GetUserRoles();
        if (roles.Count == 0)
        {
            await Clients.Caller.SendAsync("Error", "Unable to determine user role");
            return;
        }

        var bearerToken = GetBearerToken();
        var hasAccess = await _chatService.VerifyVendorConversationAccessAsync(conversationId, userId.Value, roles, bearerToken);
        if (!hasAccess)
        {
            await Clients.Caller.SendAsync("Error", "You don't have access to this chat");
            return;
        }

        var groupName = GetConversationGroupName(conversationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        await Clients.Group(groupName).SendAsync("UserJoined", new
        {
            UserId = userId,
            Role = roles.First(),
            ConversationId = conversationId
        });
    }

    public async Task LeaveVendorConversation(Guid conversationId)
    {
        var userId = GetUserId();
        if (userId == null) return;
        var groupName = GetConversationGroupName(conversationId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("UserLeft", new { UserId = userId, ConversationId = conversationId });
    }

    public async Task SendVendorMessage(Guid conversationId, string message)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            await Clients.Caller.SendAsync("Error", "Message cannot be empty");
            return;
        }

        var roles = GetUserRoles();
        if (roles.Count == 0)
        {
            await Clients.Caller.SendAsync("Error", "Unable to determine user role");
            return;
        }

        var bearerToken = GetBearerToken();
        var hasAccess = await _chatService.VerifyVendorConversationAccessAsync(conversationId, userId.Value, roles, bearerToken);
        if (!hasAccess)
        {
            await Clients.Caller.SendAsync("Error", "You don't have access to this chat");
            return;
        }

        var senderRole = roles.First();
        var senderDisplayName = GetSenderDisplayName();

        var saved = await _chatService.SaveVendorMessageAsync(conversationId, userId.Value, senderRole, senderDisplayName, message.Trim());

        var groupName = GetConversationGroupName(conversationId);
        await Clients.Group(groupName).SendAsync("ReceiveVendorMessage", new
        {
            MessageId = saved.MessageId,
            ConversationId = saved.ConversationId,
            SenderId = saved.SenderId,
            SenderRole = saved.SenderRole,
            SenderDisplayName = saved.SenderDisplayName,
            Message = saved.Message,
            SentAt = saved.SentAt
        });
    }

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return null;
        return userId;
    }

    private List<string> GetUserRoles()
    {
        var list = new List<string>();
        if (Context.User == null) return list;
        foreach (var claim in Context.User.FindAll(ClaimTypes.Role))
            if (!string.IsNullOrWhiteSpace(claim.Value)) list.Add(claim.Value.Trim());
        foreach (var claim in Context.User.FindAll("role"))
            if (!string.IsNullOrWhiteSpace(claim.Value) && !list.Contains(claim.Value.Trim(), StringComparer.OrdinalIgnoreCase))
                list.Add(claim.Value.Trim());
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

    private static string GetConversationGroupName(Guid conversationId) => $"vendor_chat_{conversationId}";

    private string? GetBearerToken()
    {
        var accessToken = Context.GetHttpContext()?.Request.Query["access_token"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(accessToken)) return accessToken;

        var authHeader = Context.GetHttpContext()?.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authHeader.Substring("Bearer ".Length).Trim();
        return null;
    }
}

