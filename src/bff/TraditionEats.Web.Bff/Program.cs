using TraditionEats.BuildingBlocks.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Insert shared configuration at the beginning so service-specific configs can override
builder.Configuration.AddSharedConfiguration(builder.Environment);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
