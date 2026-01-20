using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components;
using Radzen;
using TraditionalEats.WebApp;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Radzen services
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();

// HTTP Client for API calls
// Automatically uses current host (works for both localhost and IP access)
// For phone testing: Access the app via http://YOUR_IP:5300 and API will use the same host
var configuredApiUrl = builder.Configuration["ApiBaseUrl"];
const int apiPort = 5101; // Web BFF port

builder.Services.AddScoped(sp => 
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    var baseUri = new Uri(navigationManager.BaseUri);
    
    // If a full URL is configured, use it
    if (!string.IsNullOrEmpty(configuredApiUrl) && configuredApiUrl.StartsWith("http"))
    {
        return new HttpClient { BaseAddress = new Uri(configuredApiUrl) };
    }
    
    // Otherwise, construct API URL from current host with API port
    // This works for both localhost and IP access
    var apiBaseUri = new UriBuilder(baseUri.Scheme, baseUri.Host, apiPort, "/api/").Uri;
    return new HttpClient { BaseAddress = apiBaseUri };
});

// Add authentication if needed
// builder.Services.AddOidcAuthentication(options => { ... });

await builder.Build().RunAsync();
