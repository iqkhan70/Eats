using TraditionalEats.BuildingBlocks.Configuration;
using TraditionalEats.BuildingBlocks.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Insert shared configuration at the beginning so service-specific configs can override
builder.Configuration.AddSharedConfiguration(builder.Environment);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Redis for cart session management
builder.Services.AddRedis(builder.Configuration);

// Cart session service for guest and authenticated user cart management
builder.Services.AddScoped<TraditionalEats.BuildingBlocks.Redis.ICartSessionService, TraditionalEats.BuildingBlocks.Redis.CartSessionService>();

// JWT Authentication (optional - allows extracting customerId from token)
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? builder.Configuration["Jwt:Key"]
    ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TraditionalEats";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TraditionalEats";

// Log JWT configuration for debugging
var logger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger<Program>();
logger.LogInformation("BFF JWT Configuration - Issuer: {Issuer}, Audience: {Audience}, Secret Length: {SecretLength}",
    jwtIssuer, jwtAudience, jwtSecret?.Length ?? 0);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = !string.IsNullOrEmpty(jwtAudience), // Only validate if audience is set
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!"))
        };
        // Don't fail if token is missing (for anonymous access)
        // But still validate tokens when present
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                // Log the failure but allow the request to continue (for anonymous access)
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning(context.Exception, "JWT authentication failed: {Error}", context.Exception?.Message);
                // Don't fail the request - allow it to proceed (anonymous access)
                context.NoResult();
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                // Log successful token validation
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                logger.LogInformation("JWT token validated successfully for user: {UserId}", userId);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Add CORS for Blazor WebAssembly
// Allow connections from localhost (HTTP and HTTPS) and any IP address (for mobile browser access)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5300",
                "http://localhost:5301",
                "https://localhost:5300",
                "https://localhost:5301",
                "http://127.0.0.1:5300",
                "http://127.0.0.1:5301",
                "https://127.0.0.1:5300",
                "https://127.0.0.1:5301")
              .SetIsOriginAllowed(origin =>
              {
                  // Allow any origin that matches the pattern (for IP access from mobile)
                  // This allows http(s)://localhost:5300|5301 and http://192.168.x.x:5300, etc.
                  var uri = new Uri(origin);
                  var validHost = uri.Host == "localhost" ||
                                 uri.Host == "127.0.0.1" ||
                                 uri.Host.StartsWith("192.168.") ||
                                 uri.Host.StartsWith("10.") ||
                                 uri.Host.StartsWith("172.");
                  var validPort = uri.Port == 5300 || uri.Port == 5301;
                  var validScheme = uri.Scheme == "http" || uri.Scheme == "https";
                  return validScheme && validHost && validPort;
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
    client.Timeout = TimeSpan.FromSeconds(30);
    client.BaseAddress = new Uri(builder.Configuration["Services:RestaurantService"] ?? "http://localhost:5007");
});

builder.Services.AddHttpClient("ChatService", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.BaseAddress = new Uri(builder.Configuration["Services:ChatService"] ?? "http://localhost:5012");
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

builder.Services.AddHttpClient("OrderService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:OrderService"] ?? "http://localhost:5002");
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
app.MapGet("/", () => new
{
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
