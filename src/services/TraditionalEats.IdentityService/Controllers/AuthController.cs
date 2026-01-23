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
}

public record RegisterRequest(string Email, string? PhoneNumber, string Password, string? Role);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record AssignRoleRequest(string Email, string Role);