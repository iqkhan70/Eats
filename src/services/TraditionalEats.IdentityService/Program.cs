using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TraditionalEats.BuildingBlocks.Observability;
using TraditionalEats.BuildingBlocks.Redis;
using TraditionalEats.BuildingBlocks.Messaging;
using TraditionalEats.BuildingBlocks.Encryption;
using TraditionalEats.BuildingBlocks.Configuration;
using TraditionalEats.IdentityService.Data;
using TraditionalEats.IdentityService.Services;

var builder = WebApplication.CreateBuilder(args);

// Insert shared configuration at the beginning so service-specific configs can override
builder.Configuration.AddSharedConfiguration(builder.Environment);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// HttpClient for calling other services (CustomerService provisioning, etc.)
builder.Services.AddHttpClient();

// Database
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("IdentityDb"),
        new MySqlServerVersion(new Version(8, 0, 0))));

// Redis
builder.Services.AddRedis(builder.Configuration);

// RabbitMQ
builder.Services.AddRabbitMq(builder.Configuration);

// OpenTelemetry
builder.Services.AddOpenTelemetry("IdentityService", builder.Configuration);

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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("VendorOnly", policy => policy.RequireRole("Vendor", "Admin"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// PII Encryption (for encrypting phone in Identity)
builder.Services.AddScoped<IPiiEncryptionService, PiiEncryptionService>();

// Application services
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    db.Database.EnsureCreated();
    await SeedData.SeedAsync(db);
}

app.Run();
