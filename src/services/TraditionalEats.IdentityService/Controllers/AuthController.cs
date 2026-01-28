using Microsoft.AspNetCore.Mvc;
using TraditionalEats.IdentityService.Services;

namespace TraditionalEats.IdentityService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

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
                expiresIn = 900 // 15 minutes
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
                expiresIn = 900
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
}

public record RegisterRequest(string FirstName, string LastName, string? DisplayName, string Email, string PhoneNumber, string Password, string? Role);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record AssignRoleRequest(string Email, string Role);
public record RevokeRoleRequest(string Email, string Role);