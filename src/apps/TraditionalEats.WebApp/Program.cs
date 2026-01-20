using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
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
// For phone testing: Update appsettings.Development.json with your computer's IP address
// Example: "ApiBaseUrl": "http://192.168.1.100:5101/api/"
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5101/api/";
builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri(apiBaseUrl) 
});

// Add authentication if needed
// builder.Services.AddOidcAuthentication(options => { ... });

await builder.Build().RunAsync();
