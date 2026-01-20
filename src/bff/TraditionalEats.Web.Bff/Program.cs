using TraditionalEats.BuildingBlocks.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Insert shared configuration at the beginning so service-specific configs can override
builder.Configuration.AddSharedConfiguration(builder.Environment);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for Blazor WebAssembly
// Allow connections from localhost and any IP address (for mobile browser access)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5300", 
                "http://localhost:5301", 
                "http://127.0.0.1:5300", 
                "http://127.0.0.1:5301")
              .SetIsOriginAllowed(origin =>
              {
                  // Allow any origin that matches the pattern (for IP access from mobile)
                  // This allows http://192.168.x.x:5300, http://10.x.x.x:5300, etc.
                  var uri = new Uri(origin);
                  return uri.Scheme == "http" && 
                         (uri.Host == "localhost" || 
                          uri.Host == "127.0.0.1" || 
                          uri.Host.StartsWith("192.168.") ||
                          uri.Host.StartsWith("10.") ||
                          uri.Host.StartsWith("172.")) &&
                         (uri.Port == 5300 || uri.Port == 5301);
              })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add HTTP clients for downstream services
builder.Services.AddHttpClient("IdentityService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:IdentityService"] ?? "http://localhost:5000");
});

builder.Services.AddHttpClient("CustomerService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:CustomerService"] ?? "http://localhost:5001");
});

builder.Services.AddHttpClient("OrderService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:OrderService"] ?? "http://localhost:5002");
});

builder.Services.AddHttpClient("CatalogService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:CatalogService"] ?? "http://localhost:5003");
});

builder.Services.AddHttpClient("RestaurantService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:RestaurantService"] ?? "http://localhost:5007");
});

builder.Services.AddHttpClient("PaymentService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:PaymentService"] ?? "http://localhost:5004");
});

builder.Services.AddHttpClient("DeliveryService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:DeliveryService"] ?? "http://localhost:5005");
});

builder.Services.AddHttpClient("NotificationService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:NotificationService"] ?? "http://localhost:5006");
});

builder.Services.AddHttpClient("PromotionService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:PromotionService"] ?? "http://localhost:5008");
});

builder.Services.AddHttpClient("ReviewService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:ReviewService"] ?? "http://localhost:5009");
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Root endpoint for testing
app.MapGet("/", () => new { 
    service = "TraditionalEats.Web.Bff", 
    status = "running",
    endpoints = new[] {
        "/api/WebBff/health",
        "/api/WebBff/restaurants",
        "/api/WebBff/orders",
        "/swagger"
    }
});

app.Run();
