namespace TraditionalEats.PaymentService.Entities;

/// <summary>Archived payment for retention. Created when Job 2 moves old payments from PaymentIntents to PaymentHistory.</summary>
public class PaymentHistory
{
    public Guid PaymentIntentId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public decimal ServiceFee { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "Pending";
    public string Provider { get; set; } = "Stripe";
    public string? ProviderIntentId { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    public DateTime? CapturedAt { get; set; }
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
}
