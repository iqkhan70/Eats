using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using TraditionalEats.ChatService.Data;
using TraditionalEats.ChatService.Entities;

namespace TraditionalEats.ChatService.Services;

public class ChatService : IChatService
{
    private readonly ChatDbContext _context;
    private readonly ILogger<ChatService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public ChatService(
        ChatDbContext context,
        ILogger<ChatService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<bool> VerifyOrderAccessAsync(Guid orderId, Guid userId, string userRole)
    {
        try
        {
            // Call OrderService to verify user has access to this order
            var client = _httpClientFactory.CreateClient("OrderService");
            var response = await client.GetAsync($"/api/order/{orderId}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OrderService returned {StatusCode} for order {OrderId}", response.StatusCode, orderId);
                return false;
            }

            var orderJson = await response.Content.ReadAsStringAsync();
            using var orderDoc = System.Text.Json.JsonDocument.Parse(orderJson);
            var root = orderDoc.RootElement;

            // Extract customer ID and restaurant ID (support both camelCase and PascalCase from OrderService)
            var customerId = root.TryGetProperty("customerId", out var cProp) ? cProp.GetString()
                : root.TryGetProperty("CustomerId", out var cPProp) ? cPProp.GetString() : null;
            var restaurantId = root.TryGetProperty("restaurantId", out var rProp) ? rProp.GetString()
                : root.TryGetProperty("RestaurantId", out var rPProp) ? rPProp.GetString() : null;

            var role = userRole?.Trim() ?? string.Empty;

            // Verify access based on role (case-insensitive)
            if (string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                // Customer can only access their own orders
                return customerId != null && Guid.TryParse(customerId, out var custId) && custId == userId;
            }
            else if (string.Equals(role, "Vendor", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                // Vendor/Admin can access if they own the restaurant or are admin
                if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return true; // Admins can access all orders
                }

                // For vendors, check if they own the restaurant
                if (restaurantId != null && Guid.TryParse(restaurantId, out var restId))
                {
                    var restaurantClient = _httpClientFactory.CreateClient("RestaurantService");
                    var restaurantResponse = await restaurantClient.GetAsync($"/api/restaurant/{restId}");

                    if (restaurantResponse.IsSuccessStatusCode)
                    {
                        var restaurantJson = await restaurantResponse.Content.ReadAsStringAsync();
                        using var restaurantDoc = System.Text.Json.JsonDocument.Parse(restaurantJson);
                        var restaurantRoot = restaurantDoc.RootElement;

                        // RestaurantService uses OwnerId (not VendorId); support both camelCase and PascalCase
                        var ownerId = restaurantRoot.TryGetProperty("ownerId", out var oProp) ? oProp.GetString()
                            : restaurantRoot.TryGetProperty("OwnerId", out var oPProp) ? oPProp.GetString()
                            : restaurantRoot.TryGetProperty("vendorId", out var vProp) ? vProp.GetString()
                            : restaurantRoot.TryGetProperty("VendorId", out var vPProp) ? vPProp.GetString() : null;

                        return ownerId != null && Guid.TryParse(ownerId, out var ownerGuid) && ownerGuid == userId;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying order access for order {OrderId}, user {UserId}, role {Role}",
                orderId, userId, userRole);
            return false;
        }
    }

    public async Task<bool> VerifyOrderAccessAsync(Guid orderId, Guid userId, IEnumerable<string> userRoles)
    {
        if (userRoles == null) return false;
        foreach (var role in userRoles)
        {
            if (string.IsNullOrWhiteSpace(role)) continue;
            var hasAccess = await VerifyOrderAccessAsync(orderId, userId, role.Trim());
            if (hasAccess) return true;
        }
        return false;
    }

    public async Task<ChatMessage> SaveMessageAsync(Guid orderId, Guid senderId, string senderRole, string message)
    {
        var chatMessage = new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            OrderId = orderId,
            SenderId = senderId,
            SenderRole = senderRole,
            Message = message,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        _context.Messages.Add(chatMessage);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Saved chat message {MessageId} for order {OrderId} from {SenderId} ({Role})",
            chatMessage.MessageId, orderId, senderId, senderRole);

        return chatMessage;
    }

    public async Task<List<ChatMessage>> GetOrderMessagesAsync(Guid orderId, Guid userId, string userRole)
    {
        // Verify access first
        var hasAccess = await VerifyOrderAccessAsync(orderId, userId, userRole);
        if (!hasAccess)
        {
            _logger.LogWarning("User {UserId} ({Role}) attempted to access messages for order {OrderId} without permission",
                userId, userRole, orderId);
            return new List<ChatMessage>();
        }

        var messages = await _context.Messages
            .Where(m => m.OrderId == orderId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        return messages;
    }

    public async Task<List<ChatMessage>> GetOrderMessagesAsync(Guid orderId, Guid userId, IEnumerable<string> userRoles)
    {
        if (userRoles == null || !userRoles.Any())
        {
            _logger.LogWarning("User {UserId} attempted to access messages for order {OrderId} with no roles", userId, orderId);
            return new List<ChatMessage>();
        }
        var hasAccess = await VerifyOrderAccessAsync(orderId, userId, userRoles);
        if (!hasAccess)
        {
            _logger.LogWarning("User {UserId} attempted to access messages for order {OrderId} without permission",
                userId, orderId);
            return new List<ChatMessage>();
        }
        var messages = await _context.Messages
            .Where(m => m.OrderId == orderId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();
        return messages;
    }

    public async Task EnsureParticipantAsync(Guid orderId, Guid userId, string role)
    {
        var existing = await _context.Participants
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.UserId == userId);

        if (existing == null)
        {
            var participant = new ChatParticipant
            {
                ParticipantId = Guid.NewGuid(),
                OrderId = orderId,
                UserId = userId,
                Role = role,
                JoinedAt = DateTime.UtcNow
            };

            _context.Participants.Add(participant);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added participant {UserId} ({Role}) to order {OrderId} chat", userId, role, orderId);
        }
    }

    public async Task MarkMessagesAsReadAsync(Guid orderId, Guid userId)
    {
        var unreadMessages = await _context.Messages
            .Where(m => m.OrderId == orderId && m.SenderId != userId && !m.IsRead)
            .ToListAsync();

        foreach (var message in unreadMessages)
        {
            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
        }

        if (unreadMessages.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Marked {Count} messages as read for order {OrderId} by user {UserId}",
                unreadMessages.Count, orderId, userId);
        }
    }

    public async Task UpdateLastReadAsync(Guid orderId, Guid userId)
    {
        var participant = await _context.Participants
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.UserId == userId);

        if (participant != null)
        {
            participant.LastReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetUnreadMessageCountAsync(Guid orderId, Guid userId)
    {
        var participant = await _context.Participants
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.UserId == userId);

        if (participant == null)
        {
            return 0;
        }

        var lastReadAt = participant.LastReadAt ?? participant.JoinedAt;

        var unreadCount = await _context.Messages
            .CountAsync(m => m.OrderId == orderId
                && m.SenderId != userId
                && m.SentAt > lastReadAt);

        return unreadCount;
    }
}
