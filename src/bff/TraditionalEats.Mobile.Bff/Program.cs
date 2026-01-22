using TraditionalEats.BuildingBlocks.Configuration;
using TraditionalEats.BuildingBlocks.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Insert shared configuration at the beginning so service-specific configs can override
builder.Configuration.AddSharedConfiguration(builder.Environment);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
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
logger.LogInformation("Mobile BFF JWT Configuration - Issuer: {Issuer}, Audience: {Audience}, Secret Length: {SecretLength}", 
    jwtIssuer, jwtAudience, jwtSecret?.Length ?? 0);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = !string.IsNullOrEmpty(jwtAudience), // Only validate if audience is set (matches Web BFF)
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!"))
        };
        // Don't fail if token is missing (for anonymous access)
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning(context.Exception, "JWT authentication failed: {Error}", context.Exception?.Message);
                context.NoResult();
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                logger.LogInformation("JWT token validated successfully for user: {UserId}", userId);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Root endpoint for testing
app.MapGet("/", () => new { 
    service = "TraditionalEats.Mobile.Bff", 
    status = "running",
    endpoints = new[] {
        "/api/MobileBff/health",
        "/api/MobileBff/restaurants",
        "/api/MobileBff/orders",
        "/swagger"
    }
});

app.Run();
