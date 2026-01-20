namespace TraditionEats.CustomerService.Entities;

public class Address
{
    public Guid AddressId { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    
    // Encrypted fields
    public string Line1Enc { get; set; } = string.Empty;
    public string? Line2Enc { get; set; }
    public string CityEnc { get; set; } = string.Empty;
    public string StateEnc { get; set; } = string.Empty;
    public string ZipEnc { get; set; } = string.Empty;
    
    // Plain coordinates for geospatial queries
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? GeoHash { get; set; }
    
    public bool IsDefault { get; set; }
    public string? Label { get; set; } // "Home", "Work", etc.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
