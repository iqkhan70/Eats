namespace TraditionalEats.OrderService.Entities;

public class Order
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid RestaurantId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal DeliveryFee { get; set; }
    /// <summary>Service fee (platform): 2% of order amount before this fee, capped at $5. Kept separate for payment split (service provider vs vendor).</summary>
    public decimal ServiceFee { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Accepted, Preparing, Ready, PickedUp, InTransit, Delivered, Cancelled, Refunded
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EstimatedDeliveryAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string PaymentStatus { get; set; } = "Pending"; // Pending, Succeeded, Failed
    public string? StripePaymentIntentId { get; set; }
    public string? PaymentFailureReason { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? SpecialInstructions { get; set; }
    public string? IdempotencyKey { get; set; }

    public List<OrderItem> Items { get; set; } = new();
    public List<OrderStatusHistory> StatusHistory { get; set; } = new();
}
