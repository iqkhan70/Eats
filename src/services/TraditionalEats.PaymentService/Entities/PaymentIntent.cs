namespace TraditionalEats.PaymentService.Entities;

public class PaymentIntent
{
    public Guid PaymentIntentId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "Pending"; // Pending, Authorized, Captured, Failed, Refunded
    public string Provider { get; set; } = "Stripe";
    public string? ProviderIntentId { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AuthorizedAt { get; set; }
    public DateTime? CapturedAt { get; set; }
}
