namespace TraditionalEats.SupportService.Entities;

public class SupportMessage
{
    public Guid MessageId { get; set; }
    public Guid TicketId { get; set; }
    public SupportTicket Ticket { get; set; } = null!;
    public Guid SenderId { get; set; }
    public bool IsFromSupport { get; set; } = false;
    public string Content { get; set; } = string.Empty;
    public string AttachmentsJson { get; set; } = "[]"; // JSON array stored as string
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
