namespace TraditionalEats.ChatService.Entities;

public class ChatMessage
{
    public Guid MessageId { get; set; }
    public Guid OrderId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderRole { get; set; } = string.Empty; // "Customer", "Vendor", "Admin"
    public string? SenderDisplayName { get; set; } // User's name or email for display
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }
    /// <summary>Optional JSON metadata for extensible message types (e.g., payment requests).</summary>
    public string? MetadataJson { get; set; }
}
