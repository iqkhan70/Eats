using System.Net.Http.Headers;
using System.Net.Http;

namespace TraditionalEats.WebApp.Services;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly AuthService _authService;

    public AuthTokenHandler(AuthService authService)
    {
        _authService = authService;
        InnerHandler = new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        System.Threading.CancellationToken cancellationToken)
    {
        var token = await _authService.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
