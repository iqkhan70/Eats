namespace TraditionalEats.ChatService.Entities;

/// <summary>
/// Generic (non-order) conversation between a customer and a restaurant (vendor).
/// One conversation per (RestaurantId, CustomerId).
/// </summary>
public class VendorConversation
{
    public Guid ConversationId { get; set; }

    /// <summary>Restaurant (vendor) the customer is chatting with.</summary>
    public Guid RestaurantId { get; set; }

    /// <summary>Customer (Identity UserId).</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Optional cached display name for the customer (for vendor inbox UX).</summary>
    public string? CustomerDisplayName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }
}

