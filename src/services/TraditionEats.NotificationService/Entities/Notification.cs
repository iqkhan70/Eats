namespace TraditionEats.NotificationService.Entities;

public class Notification
{
    public Guid NotificationId { get; set; }
    public Guid? UserId { get; set; }
    public string Channel { get; set; } = string.Empty; // "email", "sms", "push"
    public string Type { get; set; } = string.Empty; // "order_confirmation", "delivery_update", etc.
    public string? TemplateId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Recipient { get; set; } // email, phone, or push token
    public string Status { get; set; } = "Pending"; // "Pending", "Sent", "Failed", "Delivered"
    public string? ErrorMessage { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
