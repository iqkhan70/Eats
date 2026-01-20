namespace TraditionEats.NotificationService.Entities;

public class NotificationTemplate
{
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "email", "sms", "push"
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? VariablesJson { get; set; } // JSON array of variable names
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
