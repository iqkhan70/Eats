using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TraditionalEats.BuildingBlocks.Observability;
using TraditionalEats.BuildingBlocks.Redis;
using TraditionalEats.BuildingBlocks.Messaging;
using TraditionalEats.BuildingBlocks.Configuration;
using TraditionalEats.RestaurantService.Data;
using TraditionalEats.RestaurantService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddSharedConfiguration(builder.Environment);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<RestaurantDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("RestaurantDb"),
        new MySqlServerVersion(new Version(8, 0, 0))));

// Redis
builder.Services.AddRedis(builder.Configuration);

// RabbitMQ
builder.Services.AddRabbitMq(builder.Configuration);

// OpenTelemetry
builder.Services.AddOpenTelemetry("RestaurantService", builder.Configuration);

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
    });

builder.Services.AddAuthorization();

// Geocoding
builder.Services.AddScoped<TraditionalEats.BuildingBlocks.Geocoding.IZipCodeLookupService, TraditionalEats.RestaurantService.Services.ZipCodeLookupService>();
builder.Services.AddScoped<TraditionalEats.BuildingBlocks.Geocoding.IGeocodingService, TraditionalEats.BuildingBlocks.Geocoding.GeocodingService>();

// Application services
builder.Services.AddScoped<IRestaurantService, RestaurantService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure database is created and seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RestaurantDbContext>();
    db.Database.EnsureCreated();
    
    // Seed initial data
    await SeedData.SeedAsync(db);
}

app.Run();
