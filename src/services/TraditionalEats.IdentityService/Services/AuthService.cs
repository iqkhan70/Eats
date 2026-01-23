using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TraditionalEats.BuildingBlocks.Redis;
using TraditionalEats.IdentityService.Data;
using TraditionalEats.IdentityService.Entities;

namespace TraditionalEats.IdentityService.Services;

public interface IAuthService
{
    Task<(string AccessToken, string RefreshToken)> LoginAsync(string email, string password, string? ipAddress);
    Task<(string AccessToken, string RefreshToken)> RefreshTokenAsync(string refreshToken);
    Task<bool> RegisterAsync(string email, string? phoneNumber, string password, string role = "Customer");
    Task LogoutAsync(string refreshToken);
    Task<bool> AssignRoleAsync(string email, string role);
}

public class AuthService : IAuthService
{
    private readonly IdentityDbContext _context;
    private readonly IRedisService _redis;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(
        IdentityDbContext context,
        IRedisService redis,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _context = context;
        _redis = redis;
        _configuration = configuration;
        _logger = logger;
        _passwordHasher = new PasswordHasher<User>();
    }

    public async Task<(string AccessToken, string RefreshToken)> LoginAsync(string email, string password, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await RecordLoginAttemptAsync(null, email, ipAddress, false, "Email or password is empty");
            throw new UnauthorizedAccessException("Email and password are required");
        }

        // Case-insensitive email lookup
        // Try direct lookup first (for users registered with lowercase email)
        var normalizedEmail = email.ToLower().Trim();
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        
        // If not found, try case-insensitive search (for backward compatibility)
        if (user == null)
        {
            var allUsers = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .ToListAsync();
            user = allUsers.FirstOrDefault(u => 
                u.Email.ToLower() == normalizedEmail || 
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        }

        if (user == null)
        {
            await RecordLoginAttemptAsync(null, email, ipAddress, false, "User not found");
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        // Check if password hash exists
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            await RecordLoginAttemptAsync(user.Id, email, ipAddress, false, "Password hash is empty");
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        // Use ASP.NET Identity PasswordHasher (same as Mental Health app)
        PasswordVerificationResult verificationResult;
        try
        {
            verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during password verification for user {UserId}", user.Id);
            await RecordLoginAttemptAsync(user.Id, email, ipAddress, false, "Password verification exception");
            throw new UnauthorizedAccessException("Invalid email or password");
        }
        
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            await RecordLoginAttemptAsync(user.Id, email, ipAddress, false, "Password verification failed");
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        // If rehash is needed (e.g., password was hashed with older algorithm), update it
        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            await _context.SaveChangesAsync();
        }

        if (user.Status != "Active" || (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow))
        {
            await RecordLoginAttemptAsync(user.Id, email, ipAddress, false, "Account locked");
            throw new UnauthorizedAccessException("Account is locked");
        }

        user.FailedLoginAttempts = 0;
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await RecordLoginAttemptAsync(user.Id, email, ipAddress, true, null);

        var accessToken = GenerateAccessToken(user);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id);

        return (accessToken, refreshToken);
    }

    public async Task<(string AccessToken, string RefreshToken)> RefreshTokenAsync(string refreshToken)
    {
        var tokenEntity = await _context.RefreshTokens
            .Include(rt => rt.User)
            .ThenInclude(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (tokenEntity == null || tokenEntity.IsRevoked || tokenEntity.ExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        var accessToken = GenerateAccessToken(tokenEntity.User);
        var newRefreshToken = await GenerateRefreshTokenAsync(tokenEntity.UserId);

        // Revoke old token
        tokenEntity.RevokedAt = DateTime.UtcNow;
        tokenEntity.RevokedReason = "Replaced by new refresh token";
        await _context.SaveChangesAsync();

        return (accessToken, newRefreshToken);
    }

    public async Task<bool> RegisterAsync(string email, string? phoneNumber, string password, string role = "Customer")
    {
        // Case-insensitive email check
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower()))
        {
            return false;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLower(), // Store email in lowercase for consistency
            PhoneNumber = phoneNumber,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        // Use ASP.NET Identity PasswordHasher (same as Mental Health app)
        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        _context.Users.Add(user);

        var roleEntity = await _context.Roles.FirstOrDefaultAsync(r => r.Name == role);
        if (roleEntity == null)
        {
            roleEntity = new Role { Id = Guid.NewGuid(), Name = role };
            _context.Roles.Add(roleEntity);
        }

        _context.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = roleEntity.Id
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var tokenEntity = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (tokenEntity != null)
        {
            tokenEntity.RevokedAt = DateTime.UtcNow;
            tokenEntity.RevokedReason = "User logout";
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> AssignRoleAsync(string email, string role)
    {
        var normalizedEmail = email.ToLower().Trim();
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null)
        {
            return false;
        }

        // Check if user already has this role
        if (user.UserRoles.Any(ur => ur.Role.Name == role))
        {
            return true; // Already has the role
        }

        // Get or create the role
        var roleEntity = await _context.Roles.FirstOrDefaultAsync(r => r.Name == role);
        if (roleEntity == null)
        {
            roleEntity = new Role { Id = Guid.NewGuid(), Name = role };
            _context.Roles.Add(roleEntity);
            await _context.SaveChangesAsync();
        }

        // Add the role to the user
        _context.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = roleEntity.Id
        });

        await _context.SaveChangesAsync();
        return true;
    }

    private string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
        };

        foreach (var userRole in user.UserRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Name));
        }

        // Get JWT secret with fallback
        var jwtSecret = _configuration["Jwt:Secret"] 
            ?? _configuration["Jwt:Key"]
            ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!"; // Default fallback
        
        if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
        {
            jwtSecret = "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
        }
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Get JWT issuer and audience with fallbacks
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "TraditionalEats";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "TraditionalEats";

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> GenerateRefreshTokenAsync(Guid userId)
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var token = Convert.ToBase64String(randomBytes);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return token;
    }

    private async Task RecordLoginAttemptAsync(Guid? userId, string? email, string? ipAddress, bool success, string? failureReason)
    {
        var attempt = new LoginAttempt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = email,
            IpAddress = ipAddress ?? "Unknown",
            Success = success,
            FailureReason = failureReason,
            AttemptedAt = DateTime.UtcNow
        };

        _context.LoginAttempts.Add(attempt);
        await _context.SaveChangesAsync();
    }
}
