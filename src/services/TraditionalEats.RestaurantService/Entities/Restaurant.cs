namespace TraditionalEats.RestaurantService.Entities;

public class Restaurant
{
    public Guid RestaurantId { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CuisineType { get; set; }
    public string? ImageUrl { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal? Rating { get; set; }
    public int? ReviewCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<DeliveryZone> DeliveryZones { get; set; } = new();
    public List<RestaurantHours> Hours { get; set; } = new();
}
