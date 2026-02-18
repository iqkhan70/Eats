namespace TraditionalEats.ChatService.Entities;

/// <summary>Message for a vendor/customer generic conversation.</summary>
public class VendorChatMessage
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }

    public Guid SenderId { get; set; }
    public string SenderRole { get; set; } = string.Empty; // "Customer", "Vendor", "Admin"
    public string? SenderDisplayName { get; set; }

    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }
}

