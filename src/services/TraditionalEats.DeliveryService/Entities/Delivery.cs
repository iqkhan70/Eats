namespace TraditionalEats.DeliveryService.Entities;

public class Delivery
{
    public Guid DeliveryId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }
    public string Status { get; set; } = "Pending"; // "Pending", "Assigned", "PickedUp", "InTransit", "Delivered", "Cancelled"
    public string? PickupAddress { get; set; }
    public string? DeliveryAddress { get; set; }
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }
    public double? DeliveryLatitude { get; set; }
    public double? DeliveryLongitude { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<DeliveryTracking> TrackingHistory { get; set; } = new();
}
