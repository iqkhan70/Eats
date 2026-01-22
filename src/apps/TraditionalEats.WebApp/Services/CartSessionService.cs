using Microsoft.JSInterop;

namespace TraditionalEats.WebApp.Services;

public class CartSessionService
{
    private readonly IJSRuntime _jsRuntime;
    private const string SESSION_ID_KEY = "cart_session_id";
    private string? _cachedSessionId;

    public CartSessionService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string> GetOrCreateSessionIdAsync()
    {
        if (_cachedSessionId != null)
        {
            return _cachedSessionId;
        }

        try
        {
            var existingSessionId = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", SESSION_ID_KEY);
            if (!string.IsNullOrEmpty(existingSessionId))
            {
                _cachedSessionId = existingSessionId;
                return existingSessionId;
            }
        }
        catch
        {
            // localStorage might not be available
        }

        // Generate new session ID
        var newSessionId = Guid.NewGuid().ToString();
        _cachedSessionId = newSessionId;

        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", SESSION_ID_KEY, newSessionId);
        }
        catch
        {
            // localStorage might not be available, but we still return the session ID
        }

        return newSessionId;
    }

    public async Task<string?> GetSessionIdAsync()
    {
        if (_cachedSessionId != null)
        {
            return _cachedSessionId;
        }

        try
        {
            var sessionId = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", SESSION_ID_KEY);
            if (!string.IsNullOrEmpty(sessionId))
            {
                _cachedSessionId = sessionId;
            }
            return sessionId;
        }
        catch
        {
            return null;
        }
    }

    public async Task ClearSessionAsync()
    {
        _cachedSessionId = null;
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", SESSION_ID_KEY);
        }
        catch
        {
            // localStorage might not be available
        }
    }
}
