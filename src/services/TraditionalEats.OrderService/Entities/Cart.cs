namespace TraditionalEats.OrderService.Entities;

public class Cart
{
    public Guid CartId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? RestaurantId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Total { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<CartItem> Items { get; set; } = new();
}
