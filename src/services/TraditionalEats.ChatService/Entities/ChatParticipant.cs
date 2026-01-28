namespace TraditionalEats.ChatService.Entities;

public class ChatParticipant
{
    public Guid ParticipantId { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty; // "Customer", "Vendor", "Admin"
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastReadAt { get; set; }
}
