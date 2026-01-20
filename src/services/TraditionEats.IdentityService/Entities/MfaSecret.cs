namespace TraditionEats.IdentityService.Entities;

public class MfaSecret
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Secret { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
