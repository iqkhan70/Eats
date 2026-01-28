using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System.Text;
using TraditionalEats.BuildingBlocks.Configuration;
using TraditionalEats.BuildingBlocks.Observability;
using TraditionalEats.BuildingBlocks.Redis;
using TraditionalEats.ChatService.Data;
using TraditionalEats.ChatService.Hubs;
using TraditionalEats.ChatService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddSharedConfiguration(builder.Environment);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SignalR
builder.Services.AddSignalR();

// CORS for SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Database
var connectionString = builder.Configuration.GetConnectionString("ChatDb")
    ?? "server=localhost;port=3306;database=traditional_eats_chat;user=root;password=UthmanBasima70;";

builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0))));

// Redis
builder.Services.AddRedis(builder.Configuration);

// OpenTelemetry
builder.Services.AddOpenTelemetry("ChatService", builder.Configuration);

// HTTP Client Factory
builder.Services.AddHttpClient();

// Configure HTTP clients for service communication
builder.Services.AddHttpClient("OrderService", client =>
{
    var baseAddress = builder.Configuration["HttpClients:OrderService:BaseAddress"] ?? "http://localhost:5002";
    client.BaseAddress = new Uri(baseAddress);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("RestaurantService", client =>
{
    var baseAddress = builder.Configuration["HttpClients:RestaurantService:BaseAddress"] ?? "http://localhost:5007";
    client.BaseAddress = new Uri(baseAddress);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// JWT Authentication - uses shared config (appsettings.Shared.json) so tokens match IdentityService
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? builder.Configuration["Jwt:Key"]
    ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";

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

        // SignalR JWT configuration
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Application services
builder.Services.AddScoped<IChatService, ChatService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// SignalR Hub
app.MapHub<OrderChatHub>("/chatHub");

app.Run();
