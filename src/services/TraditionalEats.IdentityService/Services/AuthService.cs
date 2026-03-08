using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Json;
using TraditionalEats.BuildingBlocks.Redis;
using TraditionalEats.BuildingBlocks.Encryption;
using TraditionalEats.IdentityService.Data;
using TraditionalEats.IdentityService.Entities;
using TraditionalEats.IdentityService.Exceptions;

namespace TraditionalEats.IdentityService.Services;

public interface IAuthService
{
    Task<(string AccessToken, string RefreshToken)> LoginAsync(string email, string password, string? ipAddress);
    Task<(string AccessToken, string RefreshToken)> LoginWithGoogleAsync(string idToken, string? ipAddress);
    Task<(string AccessToken, string RefreshToken)> LoginWithAppleAsync(string idToken, string? email, string? fullName, string? ipAddress);
    Task<(string AccessToken, string RefreshToken)> RefreshTokenAsync(string refreshToken);
    Task<bool> RegisterAsync(string firstName, string lastName, string? displayName, string email, string phoneNumber, string password, string role = "Customer");
    Task LogoutAsync(string refreshToken);
    Task<bool> UserExistsAsync(string email);
    Task<bool> AssignRoleAsync(string email, string role);
    Task<bool> RemoveRoleAsync(string email, string role);
    Task<List<string>> GetUserRolesAsync(string email);
    Task<ForgotPasswordResult> ForgotPasswordAsync(string email);
    Task<ResetPasswordResult> ResetPasswordAsync(string email, string token, string newPassword);
    Task<VendorRequestResult> CreateVendorApprovalRequestAsync(Guid userId, string email);
    Task<VendorRequestStatus?> GetVendorRequestStatusAsync(Guid userId);
    Task<List<VendorApprovalDto>> GetPendingVendorApprovalsAsync();
    Task<bool> ApproveVendorRequestAsync(Guid requestId, Guid adminUserId);
    /// <summary>Admin: Sync Identity users to Customer records. Creates missing Customer records.</summary>
    Task<SyncUsersToCustomersResult> SyncUsersToCustomersAsync();
}

public record SyncUsersToCustomersResult(int TotalUsers, int Created, int AlreadyExisted, int Failed, List<string> Errors);

public record VendorRequestResult(bool Success, string? Message);
public record VendorRequestStatus(string Status, DateTime RequestedAt); // Status: Pending, Approved, Rejected
public record VendorApprovalDto(Guid Id, Guid UserId, string UserEmail, string? FirstName, string? LastName, DateTime RequestedAt);

public record ForgotPasswordResult(bool Success, string Message);
public record ResetPasswordResult(bool Success, string Message);

/// <summary>Matches CustomerService GET /api/customer/by-user/{userId} response (camelCase).</summary>
internal record CustomerInfoResponse(Guid CustomerId, Guid UserId, string FirstName, string LastName, string? Email, string? PhoneNumber);

public class AuthService : IAuthService
{
    private readonly IdentityDbContext _context;
    private readonly IRedisService _redis;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPiiEncryptionService _encryption;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public AuthService(
        IdentityDbContext context,
        IRedisService redis,
        IConfiguration configuration,
        ILogger<AuthService> logger,
        IHttpClientFactory httpClientFactory,
        IPiiEncryptionService encryption,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _context = context;
        _redis = redis;
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _encryption = encryption;
        _httpContextAccessor = httpContextAccessor;
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

    public async Task<(string AccessToken, string RefreshToken)> LoginWithGoogleAsync(string idToken, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            throw new UnauthorizedAccessException("Google ID token is required");

        using var http = _httpClientFactory.CreateClient();
        var response = await http.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}");
        if (!response.IsSuccessStatusCode)
        {
            await RecordLoginAttemptAsync(null, null, ipAddress, false, "Google token verification failed");
            throw new UnauthorizedAccessException("Invalid Google token");
        }

        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var email = json.GetProperty("email").GetString();
        var sub = json.GetProperty("sub").GetString();
        if (string.IsNullOrEmpty(email))
        {
            await RecordLoginAttemptAsync(null, null, ipAddress, false, "Google token missing email");
            throw new UnauthorizedAccessException("Google token does not contain email");
        }

        var user = await GetOrCreateExternalUserAsync(email, "Google", sub, null, null);
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await RecordLoginAttemptAsync(user.Id, email, ipAddress, true, null);

        var accessToken = GenerateAccessToken(user);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id);
        return (accessToken, refreshToken);
    }

    public async Task<(string AccessToken, string RefreshToken)> LoginWithAppleAsync(string idToken, string? email, string? fullName, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            throw new UnauthorizedAccessException("Apple ID token is required");

        var appleClientId = _configuration["Apple:ClientId"] ?? _configuration["Apple:BundleId"] ?? "com.kram.mobile";
        // Support both production bundle ID and Expo Go (host.exp.Exponent) for development
        var validAudiences = new List<string> { appleClientId };
        if (!validAudiences.Contains("host.exp.Exponent"))
            validAudiences.Add("host.exp.Exponent");

        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            "https://appleid.apple.com/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());
        var config = await configManager.GetConfigurationAsync(CancellationToken.None);

        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = "https://appleid.apple.com",
            ValidAudiences = validAudiences,
            IssuerSigningKeys = config.JsonWebKeySet?.GetSigningKeys() ?? [],
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };

        var handler = new JwtSecurityTokenHandler();
        try
        {
            _ = handler.ValidateToken(idToken, validationParameters, out _);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Apple token validation failed");
            await RecordLoginAttemptAsync(null, email, ipAddress, false, "Apple token validation failed");
            throw new UnauthorizedAccessException("Invalid Apple token");
        }

        var token = handler.ReadJwtToken(idToken);
        var sub = token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var tokenEmail = token.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? email;
        User user;
        string userEmail;

        if (string.IsNullOrEmpty(tokenEmail) && string.IsNullOrEmpty(email))
        {
            // Apple omits email on subsequent sign-ins. Look up by sub (stored on first sign-in).
            if (string.IsNullOrEmpty(sub))
            {
                await RecordLoginAttemptAsync(null, null, ipAddress, false, "Apple token missing email and sub");
                throw new UnauthorizedAccessException("Apple token does not contain email. Sign in with Apple requires email on first sign-in.");
            }
            var extLogin = await _context.UserExternalLogins
                .Include(x => x.User)
                .ThenInclude(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(x => x.Provider == "Apple" && x.ProviderUserId == sub);
            if (extLogin == null)
            {
                await RecordLoginAttemptAsync(null, null, ipAddress, false, "Apple token missing email; no existing user for sub");
                throw new AppleEmailRequiredException("Enter the email for your account to link Apple Sign In.");
            }
            user = extLogin.User;
            userEmail = user.Email;
        }
        else
        {
            userEmail = tokenEmail ?? email!;
            user = await GetOrCreateExternalUserAsync(userEmail, "Apple", sub ?? userEmail ?? "apple", fullName, null);
        }
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await RecordLoginAttemptAsync(user.Id, userEmail, ipAddress, true, null);

        var accessToken = GenerateAccessToken(user);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id);
        return (accessToken, refreshToken);
    }

    private async Task<User> GetOrCreateExternalUserAsync(string email, string provider, string providerUserId, string? fullName, string? phoneNumber)
    {
        var normalizedEmail = email.ToLower().Trim();
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user != null)
        {
            // Ensure provider link exists (for Apple sub lookup on subsequent sign-ins)
            await EnsureExternalLoginAsync(user.Id, provider, providerUserId);
            // Ensure existing social-login users have a Customer record (handles past sync failures)
            var parts = fullName?.Split(' ', 2) ?? Array.Empty<string>();
            var fn = parts.Length > 0 ? parts[0] : "User";
            var ln = parts.Length > 1 ? parts[1] : provider;
            await EnsureCustomerExistsAsync(user, email, fn, ln, phoneNumber ?? "");
            return user;
        }

        var nameParts = fullName?.Split(' ', 2) ?? Array.Empty<string>();
        var firstName = nameParts.Length > 0 ? nameParts[0] : "User";
        var lastName = nameParts.Length > 1 ? nameParts[1] : provider;

        user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PhoneNumber = !string.IsNullOrWhiteSpace(phoneNumber) ? _encryption.Encrypt(phoneNumber) : null,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            PasswordHash = string.Empty,
        };

        _context.Users.Add(user);

        var roleEntity = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Customer");
        if (roleEntity == null)
        {
            roleEntity = new Role { Id = Guid.NewGuid(), Name = "Customer" };
            _context.Roles.Add(roleEntity);
        }
        _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleEntity.Id });
        _context.UserExternalLogins.Add(new UserExternalLogin { Id = Guid.NewGuid(), UserId = user.Id, Provider = provider, ProviderUserId = providerUserId });
        await _context.SaveChangesAsync();

        await EnsureCustomerExistsAsync(user, email, firstName, lastName, phoneNumber ?? "");

        return (await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == user.Id));
    }

    private async Task EnsureExternalLoginAsync(Guid userId, string provider, string providerUserId)
    {
        var exists = await _context.UserExternalLogins.AnyAsync(x => x.UserId == userId && x.Provider == provider);
        if (!exists)
        {
            _context.UserExternalLogins.Add(new UserExternalLogin { Id = Guid.NewGuid(), UserId = userId, Provider = provider, ProviderUserId = providerUserId });
            await _context.SaveChangesAsync();
        }
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

    public async Task<bool> RegisterAsync(string firstName, string lastName, string? displayName, string email, string phoneNumber, string password, string role = "Customer")
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
            PhoneNumber = !string.IsNullOrWhiteSpace(phoneNumber) ? _encryption.Encrypt(phoneNumber) : null, // Encrypt phone in Identity
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

            var response = await client.PostAsJsonAsync("/api/customer/internal", new
            {
                UserId = user.Id,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                PhoneNumber = phoneNumber,
                DisplayName = displayName
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

    /// <summary>
    /// Ensures a Customer record exists for the given Identity user. Creates one if missing.
    /// Used for social-login users (new and existing) to fix sync gaps.
    /// </summary>
    private async Task EnsureCustomerExistsAsync(User user, string email, string firstName, string lastName, string phoneNumber)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var customerServiceUrl = _configuration["Services:CustomerService"] ?? "http://localhost:5001";
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(customerServiceUrl);
                var response = await client.PostAsJsonAsync("/api/customer/internal", new
                {
                    UserId = user.Id,
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    PhoneNumber = phoneNumber,
                    DisplayName = (string?)null
                });
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    if (body.TryGetProperty("alreadyExists", out var ae) && ae.GetBoolean())
                        return; // Already had customer
                    _logger.LogInformation("Customer created for external user {Email}", email);
                    return;
                }
                _logger.LogWarning("Customer provisioning attempt {Attempt}/{Max} failed for {Email}: {Status}",
                    attempt, maxAttempts, email, response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Customer provisioning attempt {Attempt}/{Max} failed for external user {Email}",
                    attempt, maxAttempts, email);
            }
            if (attempt < maxAttempts)
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
        }
    }

    public async Task<bool> UserExistsAsync(string email)
    {
        var normalizedEmail = email.ToLower().Trim();
        return await _context.Users.AnyAsync(u => u.Email == normalizedEmail);
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

    public async Task<VendorRequestResult> CreateVendorApprovalRequestAsync(Guid userId, string email)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return new VendorRequestResult(false, "User not found");

        if (user.UserRoles.Any(ur => ur.Role.Name == "Vendor"))
            return new VendorRequestResult(false, "User is already a vendor");

        if (user.UserRoles.Any(ur => ur.Role.Name == "Admin" || ur.Role.Name == "Coordinator"))
            return new VendorRequestResult(false, "Admins and coordinators cannot request vendor status");

        var existing = await _context.VendorApprovalRequests
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Status == "Pending");

        if (existing != null)
            return new VendorRequestResult(false, "Vendor approval request already pending");

        string? firstName = null;
        string? lastName = null;
        try
        {
            var customerServiceUrl = _configuration["Services:CustomerService"] ?? "http://localhost:5001";
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(customerServiceUrl);
            var customer = await client.GetFromJsonAsync<CustomerInfoResponse>($"/api/customer/by-user/{userId}");
            if (customer != null)
            {
                firstName = customer.FirstName;
                lastName = customer.LastName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch customer name for vendor request UserId={UserId}", userId);
        }

        _context.VendorApprovalRequests.Add(new VendorApprovalRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = user.Email,
            FirstName = firstName,
            LastName = lastName,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        _logger.LogInformation("Vendor approval request created for user {Email}", user.Email);
        return new VendorRequestResult(true, null);
    }

    public async Task<VendorRequestStatus?> GetVendorRequestStatusAsync(Guid userId)
    {
        var req = await _context.VendorApprovalRequests
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync();

        if (req == null) return null;
        return new VendorRequestStatus(req.Status, req.RequestedAt);
    }

    public async Task<List<VendorApprovalDto>> GetPendingVendorApprovalsAsync()
    {
        return await _context.VendorApprovalRequests
            .Where(r => r.Status == "Pending")
            .OrderBy(r => r.RequestedAt)
            .Select(r => new VendorApprovalDto(r.Id, r.UserId, r.UserEmail, r.FirstName, r.LastName, r.RequestedAt))
            .ToListAsync();
    }

    public async Task<bool> ApproveVendorRequestAsync(Guid requestId, Guid adminUserId)
    {
        var req = await _context.VendorApprovalRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.Status == "Pending");

        if (req == null) return false;

        var success = await AssignRoleAsync(req.User.Email, "Vendor");
        if (!success)
        {
            _logger.LogWarning("Failed to assign Vendor role to {Email} when approving request {RequestId}", req.User.Email, requestId);
            return false;
        }

        req.Status = "Approved";
        req.ResolvedAt = DateTime.UtcNow;
        req.ResolvedByUserId = adminUserId;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Vendor approval request {RequestId} approved for {Email} by admin {AdminId}", requestId, req.User.Email, adminUserId);
        return true;
    }

    public async Task<SyncUsersToCustomersResult> SyncUsersToCustomersAsync()
    {
        var users = await _context.Users.ToListAsync();
        var created = 0;
        var alreadyExisted = 0;
        var failed = 0;
        var errors = new List<string>();
        var customerServiceUrl = _configuration["Services:CustomerService"] ?? "http://localhost:5001";

        foreach (var user in users)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(customerServiceUrl);
                var checkResponse = await client.GetAsync($"/api/customer/by-user/{user.Id}");
                if (checkResponse.IsSuccessStatusCode)
                {
                    alreadyExisted++;
                    continue;
                }

                var email = user.Email;
                var phoneNumber = !string.IsNullOrEmpty(user.PhoneNumber) ? _encryption.Decrypt(user.PhoneNumber) : "";
                var localPart = email.Split('@')[0];
                var firstName = localPart.Length > 0 ? char.ToUpperInvariant(localPart[0]) + localPart[1..].ToLowerInvariant() : "User";
                var lastName = "User";

                var createResponse = await client.PostAsJsonAsync("/api/customer/internal", new
                {
                    UserId = user.Id,
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    PhoneNumber = phoneNumber ?? "",
                    DisplayName = (string?)null
                });

                if (createResponse.IsSuccessStatusCode)
                {
                    var body = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    if (body.TryGetProperty("alreadyExists", out var ae) && ae.GetBoolean())
                        alreadyExisted++;
                    else
                        created++;
                }
                else
                {
                    failed++;
                    errors.Add($"{user.Email}: {createResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{user.Email}: {ex.Message}");
                _logger.LogWarning(ex, "Sync failed for user {Email}", user.Email);
            }
        }

        return new SyncUsersToCustomersResult(users.Count, created, alreadyExisted, failed, errors);
    }

    public async Task<ForgotPasswordResult> ForgotPasswordAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return new ForgotPasswordResult(false, "Email is required.");

        var normalizedEmail = email.ToLower().Trim();
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.Status == "Active");

        // Always return same message to prevent email enumeration
        var successMessage = "If an account with that email exists, a password reset link has been sent.";

        if (user == null)
            return new ForgotPasswordResult(true, successMessage);

        var resetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        await _context.SaveChangesAsync();

        // Reset link must point to the WebApp (Blazor), not IdentityService. When BFF calls us,
        // Request.Host is IdentityService (e.g. localhost:5000), so we use config only; use Origin
        // only when present (e.g. direct browser call) so we don't use the wrong host.
        string baseUrl = _configuration["AppSettings:BaseUrl"]
            ?? _configuration["AppBaseUrl"]
            ?? Environment.GetEnvironmentVariable("BASE_URL")
            ?? "https://localhost:5301";

        try
        {
            var httpContext = _httpContextAccessor?.HttpContext;
            if (httpContext?.Request != null)
            {
                var origin = httpContext.Request.Headers["Origin"].FirstOrDefault();
                if (!string.IsNullOrEmpty(origin))
                    baseUrl = origin;
            }
        }
        catch { /* use config baseUrl */ }

        var resetLink = $"{baseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(resetToken)}&email={Uri.EscapeDataString(user.Email)}";

        var subject = "Password Reset Request - TraditionalEats";
        var body = $@"<html><body>
<h2>Password Reset Request</h2>
<p>You requested to reset your password. Click the link below to reset it:</p>
<p><a href=""{resetLink}"">{resetLink}</a></p>
<p>This link expires in 1 hour.</p>
<p>If you did not request this, please ignore this email.</p>
<p>— TraditionalEats</p>
</body></html>";

        try
        {
            var notificationUrl = _configuration["Services:NotificationService"];
            if (!string.IsNullOrEmpty(notificationUrl))
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(notificationUrl);
                var payload = new { to = user.Email, subject, body };
                var response = await client.PostAsJsonAsync("/api/notification/send-email", payload);
                var responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning("Failed to send password reset email: Status={Status}, Body={Body}", response.StatusCode, responseBody);
                else
                    _logger.LogInformation("Password reset email sent to {Email}", user.Email);
            }
            else
                _logger.LogWarning("Services:NotificationService not configured; password reset email not sent. Link: {Link}", resetLink);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send password reset email; link: {Link}", resetLink);
        }

        return new ForgotPasswordResult(true, successMessage);
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(string email, string token, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
            return new ResetPasswordResult(false, "Email, token, and new password are required.");

        if (newPassword.Length < 6)
            return new ResetPasswordResult(false, "Password must be at least 6 characters long.");

        var normalizedEmail = email.ToLower().Trim();
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail
                && u.PasswordResetToken == token
                && u.Status == "Active");

        if (user == null || user.PasswordResetTokenExpiry == null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
            return new ResetPasswordResult(false, "Invalid or expired reset token. Please request a new password reset.");

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Password reset successfully for user {Email}", user.Email);
        return new ResetPasswordResult(true, "Password has been reset successfully. You can now login with your new password.");
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

        var expirationMinutes = _configuration.GetValue("Jwt:AccessTokenExpirationMinutes", 60);
        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
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
