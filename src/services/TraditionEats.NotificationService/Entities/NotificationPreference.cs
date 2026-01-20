namespace TraditionEats.NotificationService.Entities;

public class NotificationPreference
{
    public Guid PreferenceId { get; set; }
    public Guid UserId { get; set; }
    public string Channel { get; set; } = string.Empty; // "email", "sms", "push"
    public string NotificationType { get; set; } = string.Empty; // "order_confirmation", "delivery_update", etc.
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
