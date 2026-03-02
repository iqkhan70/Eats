namespace TraditionalEats.OrderService.Entities;

public class OrderStatusHistoryArchive
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ChangedAt { get; set; }
}
