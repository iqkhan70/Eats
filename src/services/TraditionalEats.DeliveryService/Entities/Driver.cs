namespace TraditionalEats.DeliveryService.Entities;

public class Driver
{
    public Guid DriverId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty; // "bike", "car", "motorcycle"
    public string VehicleLicensePlate { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public double? CurrentLatitude { get; set; }
    public double? CurrentLongitude { get; set; }
    public DateTime? LastLocationUpdate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Delivery> Deliveries { get; set; } = new();
}
