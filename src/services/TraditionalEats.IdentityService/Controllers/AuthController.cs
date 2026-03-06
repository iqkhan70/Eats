using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TraditionalEats.IdentityService.Services;

namespace TraditionalEats.IdentityService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Microsoft.AspNetCore.Authorization.AllowAnonymous] // Login, Register, Refresh, Logout must work without a token; Admin actions override with [Authorize(Roles = "Admin")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, IConfiguration configuration, ILogger<AuthController> logger)
    {
        _authService = authService;
        _configuration = configuration;
        _logger = logger;
    }

    private int GetExpiresInSeconds() => _configuration.GetValue("Jwt:AccessTokenExpirationMinutes", 60) * 60;

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var success = await _authService.RegisterAsync(
                request.FirstName,
                request.LastName,
                request.DisplayName,
                request.Email,
                request.PhoneNumber,
                request.Password,
                request.Role ?? "Customer");

            if (!success)
            {
                return BadRequest(new { message = "Email already exists" });
            }

            return Ok(new { message = "Registration successful" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed");
            return StatusCode(500, new { message = "Registration failed" });
        }
    }

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (accessToken, refreshToken) = await _authService.LoginAsync(
                request.Email,
                request.Password,
                ipAddress);

            return Ok(new
            {
                accessToken,
                refreshToken,
                expiresIn = GetExpiresInSeconds()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return StatusCode(500, new { message = "Login failed" });
        }
    }

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpPost("google")]
    public async Task<IActionResult> LoginWithGoogle([FromBody] GoogleLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.IdToken))
            return BadRequest(new { message = "ID token is required" });
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (accessToken, refreshToken) = await _authService.LoginWithGoogleAsync(request.IdToken, ipAddress);
            return Ok(new { accessToken, refreshToken, expiresIn = GetExpiresInSeconds() });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google login failed");
            return StatusCode(500, new { message = "Google login failed" });
        }
    }

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpPost("apple")]
    public async Task<IActionResult> LoginWithApple([FromBody] AppleLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.IdToken))
            return BadRequest(new { message = "ID token is required" });
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (accessToken, refreshToken) = await _authService.LoginWithAppleAsync(
                request.IdToken, request.Email, request.FullName, ipAddress);
            return Ok(new { accessToken, refreshToken, expiresIn = GetExpiresInSeconds() });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Apple login failed");
            return StatusCode(500, new { message = "Apple login failed" });
        }
    }

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var (accessToken, refreshToken) = await _authService.RefreshTokenAsync(request.RefreshToken);

            return Ok(new
            {
                accessToken,
                refreshToken,
                expiresIn = GetExpiresInSeconds()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed");
            return StatusCode(500, new { message = "Token refresh failed" });
        }
    }

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        try
        {
            await _authService.LogoutAsync(request.RefreshToken);
            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout failed");
            return StatusCode(500, new { message = "Logout failed" });
        }
    }

    [HttpPost("assign-role")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
    {
        try
        {
            var success = await _authService.AssignRoleAsync(request.Email, request.Role);

            if (!success)
            {
                return BadRequest(new { message = "User not found or role assignment failed" });
            }

            return Ok(new { message = $"Role '{request.Role}' assigned successfully to {request.Email}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Role assignment failed");
            return StatusCode(500, new { message = "Role assignment failed" });
        }
    }

    [HttpPost("revoke-role")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public async Task<IActionResult> RevokeRole([FromBody] RevokeRoleRequest request)
    {
        try
        {
            var success = await _authService.RemoveRoleAsync(request.Email, request.Role);

            if (!success)
            {
                return BadRequest(new { message = "User not found, role revocation failed, or cannot remove last role" });
            }

            return Ok(new { message = $"Role '{request.Role}' revoked successfully from {request.Email}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Role revocation failed");
            return StatusCode(500, new { message = "Role revocation failed" });
        }
    }

    [HttpGet("user-roles/{email}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUserRoles(string email)
    {
        try
        {
            var roles = await _authService.GetUserRolesAsync(email);
            return Ok(new { email, roles });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user roles");
            return StatusCode(500, new { message = "Failed to get user roles" });
        }
    }

    [HttpPost("forgot-password")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Email))
            return BadRequest(new { success = false, message = "Email is required." });

        try
        {
            var result = await _authService.ForgotPasswordAsync(request.Email);
            return Ok(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Forgot password failed for {Email}", request.Email);
            return StatusCode(500, new { success = false, message = "Unable to process password reset. Please try again later." });
        }
    }

    [HttpPost("vendor-request")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> CreateVendorRequest()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var emailClaim = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email")
            ?? User.FindFirstValue("preferred_username");

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid user" });
        if (string.IsNullOrEmpty(emailClaim))
            return BadRequest(new { message = "Email not found in token" });

        try
        {
            var result = await _authService.CreateVendorApprovalRequestAsync(userId, emailClaim);
            if (!result.Success)
                return BadRequest(new { message = result.Message ?? "Request failed" });
            return Ok(new { message = "Vendor approval request submitted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create vendor request failed");
            return StatusCode(500, new { message = "Failed to submit request" });
        }
    }

    [HttpGet("vendor-request/status")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> GetVendorRequestStatus()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid user" });

        try
        {
            var status = await _authService.GetVendorRequestStatusAsync(userId);
            if (status == null)
                return Ok(new { hasRequest = false });
            return Ok(new { hasRequest = true, status = status.Status, requestedAt = status.RequestedAt });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get vendor request status failed");
            return StatusCode(500, new { message = "Failed to get status" });
        }
    }

    [HttpGet("vendor-approvals")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPendingVendorApprovals()
    {
        try
        {
            var list = await _authService.GetPendingVendorApprovalsAsync();
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get pending vendor approvals failed");
            return StatusCode(500, new { message = "Failed to get approvals" });
        }
    }

    [HttpPost("vendor-approvals/{requestId:guid}/approve")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApproveVendorRequest(Guid requestId)
    {
        var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminIdClaim) || !Guid.TryParse(adminIdClaim, out var adminId))
            return Unauthorized(new { message = "Invalid admin" });

        try
        {
            var ok = await _authService.ApproveVendorRequestAsync(requestId, adminId);
            if (!ok)
                return NotFound(new { message = "Request not found or already resolved" });
            return Ok(new { message = "Vendor role assigned successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Approve vendor request failed");
            return StatusCode(500, new { message = "Failed to approve request" });
        }
    }

    [HttpPost("reset-password")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Email) || string.IsNullOrWhiteSpace(request?.Token) || string.IsNullOrWhiteSpace(request?.NewPassword))
            return BadRequest(new { success = false, message = "Email, token, and new password are required." });

        if (request.NewPassword != request.ConfirmPassword)
            return BadRequest(new { success = false, message = "New password and confirm password do not match." });

        try
        {
            var result = await _authService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reset password failed for {Email}", request.Email);
            return StatusCode(500, new { success = false, message = "Unable to reset password. Please try again later." });
        }
    }
}

public record RegisterRequest(string FirstName, string LastName, string? DisplayName, string Email, string PhoneNumber, string Password, string? Role);
public record LoginRequest(string Email, string Password);
public record GoogleLoginRequest(string IdToken);
public record AppleLoginRequest(string IdToken, string? Email, string? FullName);
public record RefreshTokenRequest(string RefreshToken);
public record AssignRoleRequest(string Email, string Role);
public record RevokeRoleRequest(string Email, string Role);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string Email, string NewPassword, string ConfirmPassword);