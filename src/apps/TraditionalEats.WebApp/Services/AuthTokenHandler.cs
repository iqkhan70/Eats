using System.Net.Http.Headers;
using System.Net.Http;

namespace TraditionalEats.WebApp.Services;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly AuthService _authService;
    private readonly CartSessionService _cartSessionService;

    public AuthTokenHandler(AuthService authService, CartSessionService cartSessionService)
    {
        _authService = authService;
        _cartSessionService = cartSessionService;
        InnerHandler = new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        System.Threading.CancellationToken cancellationToken)
    {
        // Add JWT token if available
        var token = await _authService.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Add cart session ID header
        var sessionId = await _cartSessionService.GetOrCreateSessionIdAsync();
        if (!string.IsNullOrEmpty(sessionId))
        {
            request.Headers.Add("X-Cart-Session-Id", sessionId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
