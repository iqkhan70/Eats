using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TraditionalEats.BuildingBlocks.Observability;
using TraditionalEats.BuildingBlocks.Redis;
using TraditionalEats.BuildingBlocks.Messaging;
using TraditionalEats.BuildingBlocks.Configuration;
using TraditionalEats.ReviewService.Data;
using TraditionalEats.ReviewService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddSharedConfiguration(builder.Environment);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<ReviewDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("ReviewDb"),
        new MySqlServerVersion(new Version(8, 0, 0))));

// Redis
builder.Services.AddRedis(builder.Configuration);

// RabbitMQ
builder.Services.AddRabbitMq(builder.Configuration);

// OpenTelemetry
builder.Services.AddOpenTelemetry("ReviewService", builder.Configuration);

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] 
    ?? builder.Configuration["Jwt:Key"]
    ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!"; // Default fallback

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Kram";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Kram";

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
    });

builder.Services.AddAuthorization();

// HTTP Client Factory for calling other services
builder.Services.AddHttpClient("RestaurantService", client =>
{
    var baseAddress = builder.Configuration["Services:RestaurantService"] ?? "http://localhost:5007";
    client.BaseAddress = new Uri(baseAddress);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Application services
builder.Services.AddScoped<IReviewService, ReviewService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure database is created and migrated
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        // Try to ensure database exists
        try
        {
            var canConnect = db.Database.CanConnect();
            if (!canConnect)
            {
                logger.LogWarning("Cannot connect to ReviewDb database. Please check connection string.");
            }
            else
            {
                // Always use Migrate to ensure schema matches migrations
                db.Database.Migrate();
                logger.LogInformation("ReviewDb database migrated");
            }
        }
        catch (Exception dbEx)
        {
            logger.LogError(dbEx, "Database connection error: {Message}", dbEx.Message);
            // Don't fail startup - let it try to connect on first request
        }
    }
}
catch (Exception ex)
{
    // Fallback if scope creation fails
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Failed to initialize ReviewDb database. Error: {Message}", ex.Message);
}

app.Run();
