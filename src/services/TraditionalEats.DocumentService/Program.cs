using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Amazon.S3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TraditionalEats.BuildingBlocks.Observability;
using TraditionalEats.BuildingBlocks.Redis;
using TraditionalEats.BuildingBlocks.Messaging;
using TraditionalEats.BuildingBlocks.Configuration;
using TraditionalEats.DocumentService.Data;
using TraditionalEats.DocumentService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddSharedConfiguration(builder.Environment);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DocumentDb"),
        new MySqlServerVersion(new Version(8, 0, 0))));

// Redis
builder.Services.AddRedis(builder.Configuration);

// RabbitMQ
builder.Services.AddRabbitMq(builder.Configuration);

// OpenTelemetry
builder.Services.AddOpenTelemetry("DocumentService", builder.Configuration);

// DigitalOcean Spaces (S3-compatible)
// Support multiple config key formats: DigitalOceanSpaces, S3 (Mental Health format)
var accessKey = (builder.Configuration["DigitalOceanSpaces:AccessKey"]
    ?? builder.Configuration["S3:AccessKey"])?.Trim();
var secretKey = (builder.Configuration["DigitalOceanSpaces:SecretKey"]
    ?? builder.Configuration["S3:SecretKey"])?.Trim();
var serviceUrl = (builder.Configuration["DigitalOceanSpaces:ServiceUrl"]
    ?? builder.Configuration["S3:ServiceUrl"])?.Trim();
var region = (builder.Configuration["DigitalOceanSpaces:Region"]
    ?? builder.Configuration["S3:Region"] ?? "sfo3")?.Trim();
var bucketName = (builder.Configuration["DigitalOceanSpaces:BucketName"]
    ?? builder.Configuration["S3:BucketName"])?.Trim();

// Create a temporary logger to log configuration
var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
var configLogger = loggerFactory.CreateLogger("S3Config");

// Debug: Log all DigitalOcean Spaces config values
configLogger.LogInformation("=== DigitalOcean Spaces Configuration ===");
configLogger.LogInformation("AccessKey: {AccessKey} (length: {Length})",
    string.IsNullOrEmpty(accessKey) ? "NULL" : accessKey.Substring(0, Math.Min(8, accessKey.Length)) + "...",
    accessKey?.Length ?? 0);
configLogger.LogInformation("SecretKey: {SecretKey} (length: {Length})",
    string.IsNullOrEmpty(secretKey) ? "NULL" : "***",
    secretKey?.Length ?? 0);
configLogger.LogInformation("BucketName: {BucketName}", bucketName ?? "NULL");
configLogger.LogInformation("Region: {Region}", region ?? "NULL");
configLogger.LogInformation("ServiceUrl: {ServiceUrl}", serviceUrl ?? "NULL");
configLogger.LogInformation("==========================================");

if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
{
    configLogger.LogInformation("Configuring DigitalOcean Spaces S3 client: Bucket={BucketName}, Region={Region}, ServiceUrl={ServiceUrl}, AccessKey={AccessKeyPrefix}...",
        bucketName, region, serviceUrl, accessKey?.Substring(0, Math.Min(8, accessKey?.Length ?? 0)));

    // DigitalOcean Spaces (S3 compatible)
    builder.Services.AddSingleton<IAmazonS3>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("S3Config");

        var accessKey = config["DigitalOceanSpaces:AccessKey"]?.Trim();
        var secretKey = config["DigitalOceanSpaces:SecretKey"]?.Trim();
        var region = config["DigitalOceanSpaces:Region"]?.Trim() ?? "sfo3";

        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("DigitalOcean Spaces credentials are missing.");

        var serviceUrl = $"https://{region}.digitaloceanspaces.com";

        logger.LogInformation("Using DigitalOcean Spaces endpoint: {ServiceUrl}", serviceUrl);

        var s3Config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true
        };

        return new AmazonS3Client(accessKey, secretKey, s3Config);
    });

}
else
{
    configLogger.LogError("DigitalOcean Spaces credentials not found! Please configure DigitalOceanSpaces or S3 settings in appsettings.json");
    throw new InvalidOperationException("DigitalOcean Spaces credentials are required. Please configure DigitalOceanSpaces:AccessKey, DigitalOceanSpaces:SecretKey, and DigitalOceanSpaces:BucketName in your configuration.");
}

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

// Application services
builder.Services.AddScoped<IS3Service, S3Service>();
builder.Services.AddScoped<IDocumentService, DocumentService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Run migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
    db.Database.Migrate();
}

app.Run();
