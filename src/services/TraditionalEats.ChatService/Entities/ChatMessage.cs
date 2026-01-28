namespace TraditionalEats.ChatService.Entities;

public class ChatMessage
{
    public Guid MessageId { get; set; }
    public Guid OrderId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderRole { get; set; } = string.Empty; // "Customer", "Vendor", "Admin"
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }
}
