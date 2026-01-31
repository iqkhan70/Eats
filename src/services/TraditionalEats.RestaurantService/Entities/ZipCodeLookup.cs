namespace TraditionalEats.RestaurantService.Entities;

/// <summary>
/// Lookup table for ZIP codes to latitude/longitude mapping.
/// Same structure as mental health app; can be seeded from the same data source.
/// </summary>
public class ZipCodeLookup
{
    public string ZipCode { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
