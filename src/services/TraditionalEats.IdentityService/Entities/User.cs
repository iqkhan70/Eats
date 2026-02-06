namespace TraditionalEats.IdentityService.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Status { get; set; } = "Active"; // Active, Locked, Suspended
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    /// <summary>Token for password reset (forgot password flow).</summary>
    public string? PasswordResetToken { get; set; }
    /// <summary>Expiry of password reset token (typically 1 hour).</summary>
    public DateTime? PasswordResetTokenExpiry { get; set; }

    public List<UserRole> UserRoles { get; set; } = new();
    public List<RefreshToken> RefreshTokens { get; set; } = new();
}
