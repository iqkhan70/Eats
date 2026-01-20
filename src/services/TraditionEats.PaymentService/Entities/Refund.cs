namespace TraditionEats.PaymentService.Entities;

public class Refund
{
    public Guid RefundId { get; set; }
    public Guid PaymentIntentId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Completed, Failed
    public string? ProviderRefundId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
