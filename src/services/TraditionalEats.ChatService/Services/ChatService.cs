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

    public async Task<bool> VerifyOrderAccessAsync(Guid orderId, Guid userId, string userRole, string? bearerToken = null)
    {
        try
        {
            // Call OrderService for minimal order metadata. We intentionally use an internal endpoint
            // because the customer-facing GET /api/order/{orderId} forbids vendors.
            var client = _httpClientFactory.CreateClient("OrderService");

            // Forward bearer token if provided (from SignalR connection)
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                client.DefaultRequestHeaders.Remove("Authorization");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {bearerToken.Trim()}");
            }

            // Forward internal API key if configured (OrderService internal endpoints support optional key)
            var internalApiKey =
                _configuration["Services:OrderService:InternalApiKey"] ?? _configuration["InternalApiKey"];
            if (!string.IsNullOrWhiteSpace(internalApiKey))
            {
                client.DefaultRequestHeaders.Remove("X-Internal-Api-Key");
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Internal-Api-Key", internalApiKey.Trim());
            }

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync($"/api/order/internal/{orderId}/metadata");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to connect to OrderService when verifying order access for order {OrderId}. Check that HttpClients:OrderService:BaseAddress is configured correctly.", orderId);
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("OrderService returned {StatusCode} for internal metadata of order {OrderId}. Response: {Response}",
                    response.StatusCode, orderId, errorContent);
                return false;
            }

            var orderJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("OrderService returned order metadata for order {OrderId}: {OrderJson}", orderId, orderJson);

            using var orderDoc = System.Text.Json.JsonDocument.Parse(orderJson);
            var root = orderDoc.RootElement;

            // Extract customer ID and restaurant ID (support both camelCase and PascalCase from OrderService)
            var customerId = root.TryGetProperty("customerId", out var cProp) ? cProp.GetString()
                : root.TryGetProperty("CustomerId", out var cPProp) ? cPProp.GetString() : null;
            var restaurantId = root.TryGetProperty("restaurantId", out var rProp) ? rProp.GetString()
                : root.TryGetProperty("RestaurantId", out var rPProp) ? rPProp.GetString() : null;

            _logger.LogInformation("VerifyOrderAccessAsync: OrderId={OrderId}, UserId={UserId}, Role={Role}, OrderCustomerId={CustomerId}, OrderRestaurantId={RestaurantId}",
                orderId, userId, userRole, customerId, restaurantId);

            var role = userRole?.Trim() ?? string.Empty;

            // Verify access based on role (case-insensitive)
            if (string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                // Customer can only access their own orders
                var hasAccess = false;
                if (customerId != null && Guid.TryParse(customerId, out var custId))
                {
                    hasAccess = custId == userId;
                    _logger.LogInformation("Customer access check: UserId={UserId} (from JWT), OrderCustomerId={CustomerId} (from order), Match={Match}, HasAccess={HasAccess}",
                        userId, custId, custId == userId, hasAccess);
                }
                else
                {
                    _logger.LogWarning("Customer access check failed: Could not parse customerId '{CustomerId}' as Guid", customerId);
                }
                return hasAccess;
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
                    HttpResponseMessage restaurantResponse;
                    try
                    {
                        restaurantResponse = await restaurantClient.GetAsync($"/api/restaurant/{restId}");
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogError(ex, "Failed to connect to RestaurantService when verifying restaurant access for restaurant {RestaurantId}. Check that HttpClients:RestaurantService:BaseAddress is configured correctly.", restId);
                        return false;
                    }

                    if (!restaurantResponse.IsSuccessStatusCode)
                    {
                        var errorBody = await restaurantResponse.Content.ReadAsStringAsync();
                        _logger.LogWarning("RestaurantService returned {StatusCode} for restaurant {RestaurantId}. Body={Body}",
                            restaurantResponse.StatusCode, restId, errorBody);
                        return false;
                    }

                    var restaurantJson = await restaurantResponse.Content.ReadAsStringAsync();
                    using var restaurantDoc = System.Text.Json.JsonDocument.Parse(restaurantJson);
                    var restaurantRoot = restaurantDoc.RootElement;

                    // RestaurantService uses OwnerId (not VendorId); support both camelCase and PascalCase
                    var ownerId = restaurantRoot.TryGetProperty("ownerId", out var oProp) ? oProp.GetString()
                        : restaurantRoot.TryGetProperty("OwnerId", out var oPProp) ? oPProp.GetString()
                        : restaurantRoot.TryGetProperty("vendorId", out var vProp) ? vProp.GetString()
                        : restaurantRoot.TryGetProperty("VendorId", out var vPProp) ? vPProp.GetString() : null;

                    var hasAccess = ownerId != null && Guid.TryParse(ownerId, out var ownerGuid) && ownerGuid == userId;
                    _logger.LogInformation("Vendor access check: UserId={UserId}, RestaurantOwnerId={OwnerId}, HasAccess={HasAccess}",
                        userId, ownerId, hasAccess);
                    return hasAccess;
                }
            }

            _logger.LogWarning("Access denied: OrderId={OrderId}, UserId={UserId}, Role={Role} - No matching access rule",
                orderId, userId, userRole);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying order access for order {OrderId}, user {UserId}, role {Role}",
                orderId, userId, userRole);
            return false;
        }
    }

    public async Task<bool> VerifyOrderAccessAsync(Guid orderId, Guid userId, IEnumerable<string> userRoles, string? bearerToken = null)
    {
        if (userRoles == null || !userRoles.Any())
        {
            _logger.LogWarning("VerifyOrderAccessAsync: No roles provided for UserId={UserId}, OrderId={OrderId}", userId, orderId);
            return false;
        }

        _logger.LogInformation("VerifyOrderAccessAsync: Checking access for UserId={UserId}, OrderId={OrderId}, Roles={Roles}",
            userId, orderId, string.Join(",", userRoles));

        foreach (var role in userRoles)
        {
            if (string.IsNullOrWhiteSpace(role)) continue;
            var hasAccess = await VerifyOrderAccessAsync(orderId, userId, role.Trim(), bearerToken);
            if (hasAccess)
            {
                _logger.LogInformation("VerifyOrderAccessAsync: Access granted via role {Role}", role);
                return true;
            }
        }

        _logger.LogWarning("VerifyOrderAccessAsync: Access denied for all roles. UserId={UserId}, OrderId={OrderId}", userId, orderId);
        return false;
    }

    public async Task<ChatMessage> SaveMessageAsync(Guid orderId, Guid senderId, string senderRole, string? senderDisplayName, string message, string? metadataJson = null)
    {
        var chatMessage = new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            OrderId = orderId,
            SenderId = senderId,
            SenderRole = senderRole,
            SenderDisplayName = senderDisplayName,
            Message = message,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            MetadataJson = metadataJson
        };

        _context.Messages.Add(chatMessage);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Saved chat message {MessageId} for order {OrderId} from {SenderId} ({Role})",
            chatMessage.MessageId, orderId, senderId, senderRole);

        return chatMessage;
    }

    public async Task<List<ChatMessage>> GetOrderMessagesAsync(Guid orderId, Guid userId, string userRole, string? bearerToken = null)
    {
        // Verify access first
        var hasAccess = await VerifyOrderAccessAsync(orderId, userId, userRole, bearerToken);
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

    public async Task<List<ChatMessage>> GetOrderMessagesAsync(Guid orderId, Guid userId, IEnumerable<string> userRoles, string? bearerToken = null)
    {
        if (userRoles == null || !userRoles.Any())
        {
            _logger.LogWarning("User {UserId} attempted to access messages for order {OrderId} with no roles", userId, orderId);
            return new List<ChatMessage>();
        }
        var hasAccess = await VerifyOrderAccessAsync(orderId, userId, userRoles, bearerToken);
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

    // ----------------------------
    // Generic vendor/customer chat
    // ----------------------------

    public async Task<VendorConversation> GetOrCreateVendorConversationAsync(Guid restaurantId, Guid customerId, string? customerDisplayName)
    {
        // One conversation per (RestaurantId, CustomerId)
        var existing = await _context.VendorConversations
            .FirstOrDefaultAsync(c => c.RestaurantId == restaurantId && c.CustomerId == customerId);

        if (existing != null)
        {
            // Refresh cached display name if we have one and it's missing
            if (!string.IsNullOrWhiteSpace(customerDisplayName) && string.IsNullOrWhiteSpace(existing.CustomerDisplayName))
            {
                existing.CustomerDisplayName = customerDisplayName.Trim();
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return existing;
        }

        var conversation = new VendorConversation
        {
            ConversationId = Guid.NewGuid(),
            RestaurantId = restaurantId,
            CustomerId = customerId,
            CustomerDisplayName = string.IsNullOrWhiteSpace(customerDisplayName) ? null : customerDisplayName.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.VendorConversations.Add(conversation);

        try
        {
            await _context.SaveChangesAsync();
            return conversation;
        }
        catch (DbUpdateException)
        {
            // Unique constraint race: fetch and return the existing record
            var raced = await _context.VendorConversations
                .FirstOrDefaultAsync(c => c.RestaurantId == restaurantId && c.CustomerId == customerId);
            if (raced != null) return raced;
            throw;
        }
    }

    public async Task<List<VendorConversation>> GetCustomerVendorConversationsAsync(Guid customerId)
    {
        return await _context.VendorConversations
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.LastMessageAt ?? c.UpdatedAt)
            .ToListAsync();
    }

    public async Task<List<VendorConversation>> GetVendorInboxAsync(Guid vendorUserId, string? bearerToken)
    {
        // Determine restaurants owned by this vendor via RestaurantService (requires vendor JWT)
        var restaurantClient = _httpClientFactory.CreateClient("RestaurantService");
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            restaurantClient.DefaultRequestHeaders.Remove("Authorization");
            restaurantClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {bearerToken.Trim()}");
        }

        HttpResponseMessage resp;
        try
        {
            resp = await restaurantClient.GetAsync("/api/restaurant/vendor/my-restaurants");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GetVendorInboxAsync: Failed to call RestaurantService for vendor {VendorUserId}", vendorUserId);
            return new List<VendorConversation>();
        }

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            _logger.LogWarning("GetVendorInboxAsync: RestaurantService returned {Status} for vendor {VendorUserId}. Body={Body}",
                resp.StatusCode, vendorUserId, body);
            return new List<VendorConversation>();
        }

        var json = await resp.Content.ReadAsStringAsync();
        var restaurantIds = new List<Guid>();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    // Support both camelCase and PascalCase
                    if (el.TryGetProperty("restaurantId", out var r) || el.TryGetProperty("RestaurantId", out r))
                    {
                        var s = r.ValueKind == System.Text.Json.JsonValueKind.String ? r.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(s) && Guid.TryParse(s, out var id)) restaurantIds.Add(id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetVendorInboxAsync: Failed to parse RestaurantService response for vendor {VendorUserId}", vendorUserId);
            return new List<VendorConversation>();
        }

        if (restaurantIds.Count == 0) return new List<VendorConversation>();

        return await _context.VendorConversations
            .Where(c => restaurantIds.Contains(c.RestaurantId))
            .OrderByDescending(c => c.LastMessageAt ?? c.UpdatedAt)
            .ToListAsync();
    }

    public async Task<List<VendorConversation>> GetAdminVendorInboxAsync(int take = 100)
    {
        take = Math.Clamp(take, 1, 500);
        return await _context.VendorConversations
            .OrderByDescending(c => c.LastMessageAt ?? c.UpdatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<bool> VerifyVendorConversationAccessAsync(Guid conversationId, Guid userId, IEnumerable<string> userRoles, string? bearerToken = null)
    {
        if (userRoles == null) return false;
        var roles = userRoles.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToList();
        if (roles.Count == 0) return false;

        if (roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)))
            return true;

        var conversation = await _context.VendorConversations.FirstOrDefaultAsync(c => c.ConversationId == conversationId);
        if (conversation == null) return false;

        if (roles.Any(r => string.Equals(r, "Customer", StringComparison.OrdinalIgnoreCase)))
        {
            return conversation.CustomerId == userId;
        }

        if (roles.Any(r => string.Equals(r, "Vendor", StringComparison.OrdinalIgnoreCase)))
        {
            return await VerifyRestaurantOwnerAsync(conversation.RestaurantId, userId, bearerToken);
        }

        return false;
    }

    public async Task<List<VendorChatMessage>> GetVendorMessagesAsync(Guid conversationId, Guid userId, IEnumerable<string> userRoles, string? bearerToken = null)
    {
        var hasAccess = await VerifyVendorConversationAccessAsync(conversationId, userId, userRoles, bearerToken);
        if (!hasAccess) return new List<VendorChatMessage>();

        return await _context.VendorMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();
    }

    public async Task<VendorChatMessage> SaveVendorMessageAsync(Guid conversationId, Guid senderId, string senderRole, string? senderDisplayName, string message, string? metadataJson = null)
    {
        var vendorMessage = new VendorChatMessage
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = senderId,
            SenderRole = senderRole,
            SenderDisplayName = senderDisplayName,
            Message = message,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            MetadataJson = metadataJson
        };

        _context.VendorMessages.Add(vendorMessage);

        var convo = await _context.VendorConversations.FirstOrDefaultAsync(c => c.ConversationId == conversationId);
        if (convo != null)
        {
            convo.UpdatedAt = DateTime.UtcNow;
            convo.LastMessageAt = vendorMessage.SentAt;
        }

        await _context.SaveChangesAsync();
        return vendorMessage;
    }

    private async Task<bool> VerifyRestaurantOwnerAsync(Guid restaurantId, Guid vendorUserId, string? bearerToken)
    {
        var restaurantClient = _httpClientFactory.CreateClient("RestaurantService");
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            restaurantClient.DefaultRequestHeaders.Remove("Authorization");
            restaurantClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {bearerToken.Trim()}");
        }

        HttpResponseMessage resp;
        try
        {
            // Public endpoint; token not strictly required but passing it is fine.
            resp = await restaurantClient.GetAsync($"/api/restaurant/{restaurantId}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "VerifyRestaurantOwnerAsync: Failed to call RestaurantService for restaurant {RestaurantId}", restaurantId);
            return false;
        }

        if (!resp.IsSuccessStatusCode)
            return false;

        var json = await resp.Content.ReadAsStringAsync();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!(root.TryGetProperty("ownerId", out var ownerEl) || root.TryGetProperty("OwnerId", out ownerEl)))
                return false;
            var ownerStr = ownerEl.ValueKind == System.Text.Json.JsonValueKind.String ? ownerEl.GetString() : null;
            return !string.IsNullOrWhiteSpace(ownerStr) && Guid.TryParse(ownerStr, out var ownerId) && ownerId == vendorUserId;
        }
        catch
        {
            return false;
        }
    }
}
