namespace TraditionEats.CustomerService.Entities;

public class CustomerPreference
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    
    public string PreferenceType { get; set; } = string.Empty; // "Dietary", "Cuisine", "SpiceLevel", etc.
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
