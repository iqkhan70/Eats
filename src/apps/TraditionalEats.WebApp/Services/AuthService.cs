using Microsoft.JSInterop;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;

namespace TraditionalEats.WebApp.Services;

public class AuthService
{
    private readonly IJSRuntime _jsRuntime;
    private const string ACCESS_TOKEN_KEY = "access_token";
    private const string REFRESH_TOKEN_KEY = "refresh_token";

    public AuthService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task StoreTokensAsync(string accessToken, string refreshToken)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", ACCESS_TOKEN_KEY, accessToken);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", REFRESH_TOKEN_KEY, refreshToken);
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", ACCESS_TOKEN_KEY);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", REFRESH_TOKEN_KEY);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        // Check if token is expired
        if (IsTokenExpired(token))
        {
            // Clear expired tokens
            await ClearTokensAsync();
            return false;
        }

        return true;
    }

    public bool IsTokenExpired(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                return true;
            }

            var jsonToken = handler.ReadJwtToken(token);
            var expClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "exp");
            
            if (expClaim == null)
            {
                return true; // No expiration claim, consider expired
            }

            // Convert Unix timestamp to DateTime
            var expirationTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim.Value)).DateTime;
            
            // Check if token is expired (with 30 second buffer to account for clock skew)
            return expirationTime <= DateTime.UtcNow.AddSeconds(30);
        }
        catch
        {
            return true; // If we can't parse the token, consider it expired
        }
    }

    public async Task<List<string>> GetUserRolesAsync()
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return new List<string>();
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            
            var roles = jsonToken.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .ToList();

            return roles;
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task<bool> IsInRoleAsync(string role)
    {
        var roles = await GetUserRolesAsync();
        return roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> IsAdminAsync()
    {
        return await IsInRoleAsync("Admin");
    }

    public async Task<bool> IsVendorAsync()
    {
        return await IsInRoleAsync("Vendor");
    }

    public async Task ClearTokensAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", ACCESS_TOKEN_KEY);
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", REFRESH_TOKEN_KEY);
    }
}
