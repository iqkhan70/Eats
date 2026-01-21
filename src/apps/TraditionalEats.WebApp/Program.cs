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
builder.Services.AddScoped<CartService>();

// HTTP Client for API calls
// Automatically uses current host (works for both localhost and IP access)
// For phone testing: Access the app via http://YOUR_IP:5300 and API will use the same host
var configuredApiUrl = builder.Configuration["ApiBaseUrl"];
const int apiPort = 5101; // Web BFF port

// HTTP Client that automatically adds auth tokens
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    var authService = sp.GetRequiredService<AuthService>();
    var baseUri = new Uri(navigationManager.BaseUri);
    
    // If a full URL is configured, use it
    Uri apiBaseUri;
    if (!string.IsNullOrEmpty(configuredApiUrl) && configuredApiUrl.StartsWith("http"))
    {
        apiBaseUri = new Uri(configuredApiUrl);
    }
    else
    {
        // Otherwise, construct API URL from current host with API port
        // This works for both localhost and IP access
        apiBaseUri = new UriBuilder(baseUri.Scheme, baseUri.Host, apiPort, "/api/").Uri;
    }
    
    // Create HttpClient with message handler that adds auth tokens
    var handler = new AuthTokenHandler(authService);
    return new HttpClient(handler) { BaseAddress = apiBaseUri };
});

// Add authentication if needed
// builder.Services.AddOidcAuthentication(options => { ... });

await builder.Build().RunAsync();
