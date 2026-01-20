namespace TraditionalEats.RestaurantService.Entities;

public class DeliveryZone
{
    public Guid ZoneId { get; set; }
    public Guid RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? PolygonCoordinatesJson { get; set; } // JSON array of {lat, lng} points
    public decimal DeliveryFee { get; set; }
    public int EstimatedMinutes { get; set; }
    public decimal? MinimumOrderAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
