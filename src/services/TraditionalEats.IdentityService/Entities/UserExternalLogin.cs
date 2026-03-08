namespace TraditionalEats.IdentityService.Entities;

/// <summary>
/// Links a User to an external provider (Apple, Google). Used to look up users by provider user ID (e.g. Apple's sub)
/// when the provider omits email on subsequent sign-ins.
/// </summary>
public class UserExternalLogin
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = string.Empty; // "Apple", "Google"
    public string ProviderUserId { get; set; } = string.Empty; // e.g. Apple's sub claim
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
