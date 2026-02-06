namespace TraditionalEats.PaymentService.Entities;

/// <summary>
/// Stripe Connect (Standard) state per vendor (Identity UserId = restaurant owner).
/// One connected account per vendor; used for all their restaurants.
/// </summary>
public class Vendor
{
    public Guid VendorId { get; set; }
    /// <summary>Identity UserId (restaurant OwnerId).</summary>
    public Guid UserId { get; set; }
    /// <summary>Stripe connected account id (acct_...).</summary>
    public string? StripeAccountId { get; set; }
    /// <summary>Pending = onboarding not done; Complete = charges_enabled & payouts_enabled; Restricted = limited.</summary>
    public string StripeOnboardingStatus { get; set; } = "Pending"; // Pending, Complete, Restricted
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
