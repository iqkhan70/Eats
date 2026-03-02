namespace TraditionalEats.OrderService.Entities;

/// <summary>Archived order for retention. Created when Job 2 moves old orders from Orders to OrderHistory.</summary>
public class OrderHistory
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid RestaurantId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? EstimatedDeliveryAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public string? StripePaymentIntentId { get; set; }
    public string? PaymentFailureReason { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? SpecialInstructions { get; set; }
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;

    public List<OrderItemHistory> Items { get; set; } = new();
    public List<OrderStatusHistoryArchive> StatusHistory { get; set; } = new();
}
