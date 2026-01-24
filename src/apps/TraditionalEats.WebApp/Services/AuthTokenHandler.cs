using System.Net.Http.Headers;
using System.Net.Http;
using Microsoft.AspNetCore.Components;

namespace TraditionalEats.WebApp.Services;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly AuthService _authService;
    private readonly CartSessionService _cartSessionService;
    private readonly NavigationManager _navigationManager;

    public AuthTokenHandler(AuthService authService, CartSessionService cartSessionService, NavigationManager navigationManager)
    {
        _authService = authService;
        _cartSessionService = cartSessionService;
        _navigationManager = navigationManager;
        InnerHandler = new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        System.Threading.CancellationToken cancellationToken)
    {
        // Check if token is expired before making the request
        var token = await _authService.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            // Check if token is expired
            if (_authService.IsTokenExpired(token))
            {
                // Clear expired tokens
                await _authService.ClearTokensAsync();
                
                // Throw exception to let UI handle redirect
                throw new SessionExpiredException();
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Add cart session ID header
        var sessionId = await _cartSessionService.GetOrCreateSessionIdAsync();
        if (!string.IsNullOrEmpty(sessionId))
        {
            request.Headers.Add("X-Cart-Session-Id", sessionId);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Handle 401 Unauthorized responses (token expired or invalid)
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // Clear tokens
            await _authService.ClearTokensAsync();
            
            // Throw exception to let UI handle redirect
            throw new SessionExpiredException();
        }

        return response;
    }
}
