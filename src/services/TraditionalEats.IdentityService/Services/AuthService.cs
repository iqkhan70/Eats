using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
}

public class AuthService : IAuthService
{
    private readonly IdentityDbContext _context;
    private readonly IRedisService _redis;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

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
    }

    public async Task<(string AccessToken, string RefreshToken)> LoginAsync(string email, string password, string? ipAddress)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            await RecordLoginAttemptAsync(null, email, ipAddress, false, "Invalid credentials");
            throw new UnauthorizedAccessException("Invalid email or password");
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
        if (await _context.Users.AnyAsync(u => u.Email == email))
        {
            return false;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PhoneNumber = phoneNumber,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

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

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured")));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
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
