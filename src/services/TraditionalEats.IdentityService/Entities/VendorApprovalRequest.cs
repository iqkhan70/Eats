namespace TraditionalEats.IdentityService.Entities;

/// <summary>Vendor approval work item. Created when a user requests to become a vendor.</summary>
public class VendorApprovalRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    /// <summary>User's first name (from CustomerService at request time).</summary>
    public string? FirstName { get; set; }
    /// <summary>User's last name (from CustomerService at request time).</summary>
    public string? LastName { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }

    public User User { get; set; } = null!;
}
