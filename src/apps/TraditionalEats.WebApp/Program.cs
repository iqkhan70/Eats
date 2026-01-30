using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components;
using Radzen;
using TraditionalEats.WebApp;
using TraditionalEats.WebApp.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Radzen services
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();

// Application services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CartSessionService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<ChatService>();

// HTTP Client for API calls
// When app is on HTTPS we must call the BFF over HTTPS to avoid "Failed to fetch" (mixed content).
// BFF HTTP = 5101, BFF HTTPS = 5143 (5143 avoids conflict with other services on 51xx).
var configuredApiUrl = builder.Configuration["ApiBaseUrl"];
var configuredApiUrlHttps = builder.Configuration["ApiBaseUrlHttps"];
const int apiPortHttp = 5101;
const int apiPortHttps = 5143;

// HTTP Client that automatically adds auth tokens
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    var authService = sp.GetRequiredService<AuthService>();
    var baseUri = new Uri(navigationManager.BaseUri);
    var isHttps = baseUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

    Uri apiBaseUri;
    if (isHttps && !string.IsNullOrEmpty(configuredApiUrlHttps) && configuredApiUrlHttps.StartsWith("https"))
    {
        apiBaseUri = new Uri(configuredApiUrlHttps);
    }
    else if (!isHttps && !string.IsNullOrEmpty(configuredApiUrl) && configuredApiUrl.StartsWith("http"))
    {
        apiBaseUri = new Uri(configuredApiUrl);
    }
    else
    {
        // Build from current host: HTTPS page -> HTTPS BFF (5102), HTTP page -> HTTP BFF (5101)
        var port = isHttps ? apiPortHttps : apiPortHttp;
        var scheme = isHttps ? "https" : "http";
        apiBaseUri = new UriBuilder(scheme, baseUri.Host, port, "/api/").Uri;
    }

    // Create HttpClient with message handler that adds auth tokens and cart session ID
    var cartSessionService = sp.GetRequiredService<CartSessionService>();
    var handler = new AuthTokenHandler(authService, cartSessionService, navigationManager);
    return new HttpClient(handler) { BaseAddress = apiBaseUri };
});

// Add authentication if needed
// builder.Services.AddOidcAuthentication(options => { ... });

await builder.Build().RunAsync();
