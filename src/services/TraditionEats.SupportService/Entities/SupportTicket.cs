namespace TraditionEats.SupportService.Entities;

public class SupportTicket
{
    public Guid TicketId { get; set; }
    public Guid UserId { get; set; }
    public Guid? OrderId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "order_issue", "payment", "delivery", "general"
    public string Status { get; set; } = "Open"; // "Open", "InProgress", "Resolved", "Closed"
    public string Priority { get; set; } = "Medium"; // "Low", "Medium", "High", "Urgent"
    public Guid? AssignedTo { get; set; } // Support agent ID
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public List<SupportMessage> Messages { get; set; } = new();
}
