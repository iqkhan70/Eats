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
        var sentAuthorization = false;
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
            sentAuthorization = true;
        }

        // Add cart session ID header
        var sessionId = await _cartSessionService.GetOrCreateSessionIdAsync();
        if (!string.IsNullOrEmpty(sessionId))
        {
            request.Headers.Add("X-Cart-Session-Id", sessionId);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // 401 when we sent a token: session expired or revoked — clear and redirect.
        // 401 with no token (e.g. place-order requires login): return response so UI can show "sign in" instead of "session expired".
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && sentAuthorization)
        {
            await _authService.ClearTokensAsync();
            throw new SessionExpiredException();
        }

        return response;
    }
}
