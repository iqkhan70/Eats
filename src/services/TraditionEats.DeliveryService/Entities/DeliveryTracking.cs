namespace TraditionEats.DeliveryService.Entities;

public class DeliveryTracking
{
    public Guid TrackingId { get; set; }
    public Guid DeliveryId { get; set; }
    public Delivery Delivery { get; set; } = null!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
