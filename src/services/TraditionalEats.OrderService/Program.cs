using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TraditionalEats.BuildingBlocks.Observability;
using TraditionalEats.BuildingBlocks.Redis;
using TraditionalEats.BuildingBlocks.Messaging;
using TraditionalEats.BuildingBlocks.Configuration;
using TraditionalEats.OrderService.Data;
using TraditionalEats.OrderService.Services;
using Microsoft.AspNetCore.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using TraditionalEats.OrderService.Entities;

var builder = WebApplication.CreateBuilder(args);

// Insert shared configuration at the beginning so service-specific configs can override
builder.Configuration.AddSharedConfiguration(builder.Environment);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    })
    .AddOData(options => options
        .Select()
        .Filter()
        .OrderBy()
        .Count()
        .SetMaxTop(1000)
        .AddRouteComponents("odata", GetEdmModel())
    );
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("OrderDb"),
        new MySqlServerVersion(new Version(8, 0, 0))));

// Redis
builder.Services.AddRedis(builder.Configuration);

// RabbitMQ
builder.Services.AddRabbitMq(builder.Configuration);

// OpenTelemetry
builder.Services.AddOpenTelemetry("OrderService", builder.Configuration);

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] 
    ?? builder.Configuration["Jwt:Key"]
    ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!"; // Default fallback

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TraditionalEats";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TraditionalEats";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
        // Always try to validate tokens, even for anonymous endpoints
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning(context.Exception, "JWT authentication failed in OrderService: {Error}", context.Exception?.Message);
                // Don't fail the request - allow anonymous access
                context.NoResult();
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                logger.LogInformation("JWT token validated successfully in OrderService for user: {UserId}", userId);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Application services
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureCreated();
}

app.Run();

/// <summary>
/// Creates the Entity Data Model (EDM) for OData
/// </summary>
static IEdmModel GetEdmModel()
{
    var builder = new ODataConventionModelBuilder();

    // Orders - main entity set
    var orderSet = builder.EntitySet<Order>("Orders");
    orderSet.EntityType.HasKey(o => o.OrderId);
    
    // Register related entity sets (needed for navigation properties)
    var orderItemSet = builder.EntitySet<OrderItem>("OrderItems");
    orderItemSet.EntityType.HasKey(oi => oi.OrderItemId);
    
    var orderStatusHistorySet = builder.EntitySet<OrderStatusHistory>("OrderStatusHistories");
    orderStatusHistorySet.EntityType.HasKey(osh => osh.Id);
    
    // Configure navigation properties - OData will serialize them when using .Include()
    // Note: We're not using $expand, but the data will be included via .Include() in the controller
    orderSet.EntityType.HasMany(o => o.Items);
    orderSet.EntityType.HasMany(o => o.StatusHistory);

    return builder.GetEdmModel();
}
