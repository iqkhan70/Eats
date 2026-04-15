namespace TraditionalEats.NotificationService.Entities;

public class DevicePushToken
{
    public Guid DevicePushTokenId { get; set; }
    public Guid UserId { get; set; }
    public string PushToken { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastRegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
