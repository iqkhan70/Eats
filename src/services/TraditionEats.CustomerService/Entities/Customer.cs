namespace TraditionEats.CustomerService.Entities;

public class Customer
{
    public Guid CustomerId { get; set; }
    public Guid UserId { get; set; }
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public CustomerPII? PII { get; set; }
    public List<Address> Addresses { get; set; } = new();
    public List<CustomerPreference> Preferences { get; set; } = new();
}
