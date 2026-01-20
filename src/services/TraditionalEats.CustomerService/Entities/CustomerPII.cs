namespace TraditionalEats.CustomerService.Entities;

public class CustomerPII
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    
    // Encrypted fields
    public string FirstNameEnc { get; set; } = string.Empty;
    public string LastNameEnc { get; set; } = string.Empty;
    public string? PhoneEnc { get; set; }
    public string EmailEnc { get; set; } = string.Empty;
    
    // Hashed fields for search
    public string? PhoneHash { get; set; }
    public string EmailHash { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
