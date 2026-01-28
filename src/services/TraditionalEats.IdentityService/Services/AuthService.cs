using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Json;
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
    Task<bool> RemoveRoleAsync(string email, string role);
    Task<List<string>> GetUserRolesAsync(string email);
}

public class AuthService : IAuthService
{
    private readonly IdentityDbContext _context;
    private readonly IRedisService _redis;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthService(
        IdentityDbContext context,
        IRedisService redis,
        IConfiguration configuration,
        ILogger<AuthService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _redis = redis;
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
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

        // Create corresponding customer profile (CustomerService owns customer PII/profile).
        // This is what NotificationService uses later to send "Order Ready" email/SMS.
        try
        {
            var customerServiceUrl = _configuration["Services:CustomerService"] ?? "http://localhost:5001";
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(customerServiceUrl);

            var firstName = email.Split('@', 2)[0]; // basic default; can be improved later
            var lastName = string.Empty;

            var response = await client.PostAsJsonAsync("/api/customer/internal", new
            {
                UserId = user.Id,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                PhoneNumber = phoneNumber
            });

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Customer provisioning failed: Status={Status}, Body={Body}", response.StatusCode, body);
                // Keep systems consistent: roll back the identity user creation
                _context.UserRoles.RemoveRange(_context.UserRoles.Where(ur => ur.UserId == user.Id));
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                throw new InvalidOperationException("Customer provisioning failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Customer provisioning failed during registration");
            throw;
        }

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

    public async Task<bool> RemoveRoleAsync(string email, string role)
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

        // Find the user role to remove
        var userRole = user.UserRoles.FirstOrDefault(ur => ur.Role.Name == role);
        if (userRole == null)
        {
            return true; // User doesn't have this role, consider it successful
        }

        // Don't allow removing the last role (user must have at least one role)
        if (user.UserRoles.Count == 1)
        {
            _logger.LogWarning("Cannot remove last role from user {Email}. User must have at least one role.", email);
            return false;
        }

        // Remove the role
        _context.UserRoles.Remove(userRole);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role '{Role}' removed from user {Email}", role, email);
        return true;
    }

    public async Task<List<string>> GetUserRolesAsync(string email)
    {
        var normalizedEmail = email.ToLower().Trim();
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null)
        {
            return new List<string>();
        }

        return user.UserRoles.Select(ur => ur.Role.Name).ToList();
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
