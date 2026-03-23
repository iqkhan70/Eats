using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TraditionalEats.Contracts.Events;

namespace TraditionalEats.NotificationService.Services;

public class OrderStatusEventHandler : BackgroundService
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderStatusEventHandler> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConnectionFactory _factory;

    public OrderStatusEventHandler(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<OrderStatusEventHandler> logger)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;

        _factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:UserName"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest"
        };

        // Try to connect, but don't fail if RabbitMQ is unavailable
        TryConnect();
    }

    private void TryConnect()
    {
        try
        {
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare exchange and queue
            _channel.ExchangeDeclare("tradition-eats", ExchangeType.Topic, durable: true);
            var queueName = _channel.QueueDeclare("notification-service-order-status", durable: true, exclusive: false, autoDelete: false).QueueName;
            _channel.QueueBind(queueName, "tradition-eats", "order.status.changed", null);
            _channel.BasicQos(0, 1, false);

            _logger.LogInformation("RabbitMQ connection established for OrderStatusEventHandler");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to RabbitMQ. Order status event handling will be disabled. HostName={HostName}, Port={Port}", 
                _factory.HostName, _factory.Port);
            // Don't throw - allow service to start without RabbitMQ
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // If RabbitMQ is not available, wait and retry periodically
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_channel == null || _connection == null || !_connection.IsOpen)
            {
                _logger.LogInformation("RabbitMQ not available. Retrying connection in 30 seconds...");
                try
                {
                    await Task.Delay(30000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                TryConnect();
                continue;
            }

            try
            {
                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (model, ea) =>
                {
                    if (_channel == null) return;

                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var routingKey = ea.RoutingKey;

                    try
                    {
                        _logger.LogInformation("Received message: {RoutingKey}, {Message}", routingKey, message);

                        if (routingKey == "order.status.changed")
                        {
                            var statusChangedEvent = JsonSerializer.Deserialize<OrderStatusChangedEvent>(message);
                            if (statusChangedEvent != null)
                            {
                                await HandleOrderStatusChangedAsync(statusChangedEvent);
                            }
                        }

                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message: {Message}", message);
                        if (_channel != null && _channel.IsOpen)
                        {
                            _channel.BasicNack(ea.DeliveryTag, false, true); // Requeue on error
                        }
                    }
                };

                _channel.BasicConsume("notification-service-order-status", false, consumer);
                _logger.LogInformation("OrderStatusEventHandler started, waiting for messages...");

                // Keep running until cancellation or connection lost
                while (!stoppingToken.IsCancellationRequested && _connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen)
                {
                    await Task.Delay(1000, stoppingToken);
                }

                if (_connection == null || !_connection.IsOpen)
                {
                    _logger.LogWarning("RabbitMQ connection lost. Will retry...");
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when host is stopping; exit gracefully
                _logger.LogInformation("OrderStatusEventHandler is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OrderStatusEventHandler. Will retry...");
                _connection?.Dispose();
                _channel?.Dispose();
                _connection = null;
                _channel = null;
                try
                {
                    await Task.Delay(30000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task HandleOrderStatusChangedAsync(OrderStatusChangedEvent evt)
    {
        _logger.LogInformation("Handling order status changed: OrderId={OrderId}, OldStatus={OldStatus}, NewStatus={NewStatus}",
            evt.OrderId, evt.OldStatus, evt.NewStatus);

        if (evt.NewStatus != "Ready" && evt.NewStatus != "Completed")
        {
            _logger.LogInformation("Status '{NewStatus}' does not trigger notifications, skipping.", evt.NewStatus);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();

        CustomerDto? customer = null;
        try { customer = await customerService.GetCustomerAsync(evt.CustomerId); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to fetch customer {CustomerId}, notifications skipped", evt.CustomerId); }

        if (customer == null)
        {
            _logger.LogWarning("Customer not found: CustomerId={CustomerId}, skipping notifications", evt.CustomerId);
            return;
        }

        var orderShort = evt.OrderId.ToString()[..8];

        RestaurantInfoDto? restaurant = null;
        try { restaurant = await scope.ServiceProvider.GetRequiredService<IRestaurantServiceClient>().GetRestaurantAsync(evt.RestaurantId); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to fetch restaurant {RestaurantId} for notification", evt.RestaurantId); }

        if (evt.NewStatus == "Ready")
        {
            await SendOrderReadyNotificationsAsync(notificationService, evt, customer, orderShort, restaurant);
        }
        else if (evt.NewStatus == "Completed")
        {
            var orderClient = scope.ServiceProvider.GetRequiredService<IOrderServiceClient>();
            await SendOrderCompleteReceiptAsync(notificationService, orderClient, evt, customer, orderShort, restaurant);
        }
    }

    private async Task SendOrderReadyNotificationsAsync(
        INotificationService notificationService, OrderStatusChangedEvent evt, CustomerDto customer, string orderShort, RestaurantInfoDto? restaurant)
    {
        var restaurantName = restaurant?.Name ?? "the restaurant";
        var addressLine = !string.IsNullOrWhiteSpace(restaurant?.Address) ? restaurant.Address : null;

        var subject = $"Your Order is Ready for Pickup at {restaurantName}!";
        var emailBody = $@"
<html>
<body style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;"">
    <h2 style=""color: #f97316;"">Your Order is Ready!</h2>
    <p>Hi {customer.FirstName},</p>
    <p>Your order <strong>#{orderShort}</strong> from <strong>{restaurantName}</strong> is now ready for pickup.</p>
    {(addressLine != null ? $"<p style=\"margin-top:8px;\"><strong>Pickup at:</strong> {addressLine}</p>" : "")}
    <p>Please come to the restaurant to collect your order.</p>
    <br/>
    <p style=""color: #666;"">Thank you for choosing Kram!</p>
</body>
</html>";

        var smsBody = addressLine != null
            ? $"Hi {customer.FirstName}, your order #{orderShort} from {restaurantName} is ready for pickup at {addressLine}. - Kram"
            : $"Hi {customer.FirstName}, your order #{orderShort} from {restaurantName} is ready for pickup! - Kram";

        await TrySendEmailAsync(notificationService, evt.CustomerId, evt.OrderId, "OrderReady", subject, emailBody, customer.Email);
        await TrySendSmsAsync(notificationService, evt.CustomerId, evt.OrderId, "OrderReady", "Order Ready", smsBody, customer.PhoneNumber);
    }

    private async Task SendOrderCompleteReceiptAsync(
        INotificationService notificationService, IOrderServiceClient orderClient, OrderStatusChangedEvent evt, CustomerDto customer, string orderShort, RestaurantInfoDto? restaurant)
    {
        var restaurantName = restaurant?.Name ?? "Restaurant";
        var addressLine = !string.IsNullOrWhiteSpace(restaurant?.Address) ? restaurant.Address : null;

        OrderDetailsDto? orderDetails = null;
        try { orderDetails = await orderClient.GetOrderDetailsAsync(evt.OrderId); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to fetch order details for receipt: OrderId={OrderId}", evt.OrderId); }

        var itemsHtml = "";
        var totalStr = "N/A";

        if (orderDetails != null)
        {
            totalStr = $"${orderDetails.Total:F2}";
            var rows = orderDetails.Items.Select(i =>
                $"<tr><td style=\"padding:4px 8px;\">{i.Name}</td><td style=\"padding:4px 8px;text-align:center;\">{i.Quantity}</td><td style=\"padding:4px 8px;text-align:right;\">${i.TotalPrice:F2}</td></tr>");
            itemsHtml = string.Join("\n", rows);
        }

        var subject = $"Your Kram Receipt - Order #{orderShort} from {restaurantName}";
        var emailBody = $@"
<html>
<body style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;"">
    <h2 style=""color: #f97316;"">Order Complete - Receipt</h2>
    <p>Hi {customer.FirstName},</p>
    <p>Your order <strong>#{orderShort}</strong> from <strong>{restaurantName}</strong> is complete. Here's your receipt:</p>
    {(addressLine != null ? $"<p style=\"color:#666;\"><strong>Restaurant:</strong> {restaurantName}, {addressLine}</p>" : "")}
    <table style=""width:100%; border-collapse:collapse; margin:16px 0;"">
        <thead>
            <tr style=""border-bottom:2px solid #eee;"">
                <th style=""padding:8px; text-align:left;"">Item</th>
                <th style=""padding:8px; text-align:center;"">Qty</th>
                <th style=""padding:8px; text-align:right;"">Price</th>
            </tr>
        </thead>
        <tbody>
            {itemsHtml}
        </tbody>
    </table>
    {(orderDetails != null ? $@"
    <table style=""width:100%; margin-top:8px;"">
        <tr><td style=""padding:2px 8px;"">Subtotal</td><td style=""padding:2px 8px; text-align:right;"">${orderDetails.Subtotal:F2}</td></tr>
        <tr><td style=""padding:2px 8px;"">Tax</td><td style=""padding:2px 8px; text-align:right;"">${orderDetails.Tax:F2}</td></tr>
        {(orderDetails.DeliveryFee > 0 ? $"<tr><td style=\"padding:2px 8px;\">Delivery</td><td style=\"padding:2px 8px; text-align:right;\">${orderDetails.DeliveryFee:F2}</td></tr>" : "")}
        {(orderDetails.ServiceFee > 0 ? $"<tr><td style=\"padding:2px 8px;\">Service Fee</td><td style=\"padding:2px 8px; text-align:right;\">${orderDetails.ServiceFee:F2}</td></tr>" : "")}
        <tr style=""border-top:2px solid #333; font-weight:bold;""><td style=""padding:8px;"">Total</td><td style=""padding:8px; text-align:right;"">{totalStr}</td></tr>
    </table>" : "")}
    <br/>
    <p style=""color: #666;"">Thank you for choosing Kram!</p>
</body>
</html>";

        var smsBody = orderDetails != null
            ? $"Hi {customer.FirstName}, your order #{orderShort} from {restaurantName} is complete! Total: {totalStr}. Thank you for choosing Kram!"
            : $"Hi {customer.FirstName}, your order #{orderShort} from {restaurantName} is complete! Thank you for choosing Kram!";

        await TrySendEmailAsync(notificationService, evt.CustomerId, evt.OrderId, "OrderComplete", subject, emailBody, customer.Email);
        await TrySendSmsAsync(notificationService, evt.CustomerId, evt.OrderId, "OrderComplete", "Order Complete", smsBody, customer.PhoneNumber);
    }

    private async Task TrySendEmailAsync(
        INotificationService svc, Guid customerId, Guid orderId, string type, string subject, string body, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogInformation("Skipping email ({Type}): no email for customer {CustomerId}", type, customerId);
            return;
        }
        try
        {
            var sent = await svc.SendNotificationAsync(new SendNotificationDto(customerId, "email", type, subject, body, email, null));
            _logger.LogInformation("Email {Type}: OrderId={OrderId}, Sent={Sent}", type, orderId, sent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email {Type} failed for OrderId={OrderId}, ignoring", type, orderId);
        }
    }

    private async Task TrySendSmsAsync(
        INotificationService svc, Guid customerId, Guid orderId, string type, string subject, string body, string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            _logger.LogInformation("Skipping SMS ({Type}): no phone for customer {CustomerId}", type, customerId);
            return;
        }
        try
        {
            var sent = await svc.SendNotificationAsync(new SendNotificationDto(customerId, "sms", type, subject, body, phone, null));
            _logger.LogInformation("SMS {Type}: OrderId={OrderId}, Sent={Sent}", type, orderId, sent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMS {Type} failed for OrderId={OrderId}, ignoring", type, orderId);
        }
    }

    public override void Dispose()
    {
        try
        {
            if (_channel != null && _channel.IsOpen)
            {
                _channel.Close();
            }
            if (_connection != null && _connection.IsOpen)
            {
                _connection.Close();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing RabbitMQ connection");
        }
        finally
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}

// Temporary interface for customer service - will be replaced with actual service call
public interface ICustomerService
{
    Task<CustomerDto?> GetCustomerAsync(Guid customerId);
}

public class CustomerService : ICustomerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(HttpClient httpClient, ILogger<CustomerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CustomerDto?> GetCustomerAsync(Guid customerId)
    {
        try
        {
            // Order.CustomerId is the identity UserId; CustomerService exposes by-user/{userId}
            var response = await _httpClient.GetAsync($"/api/customer/by-user/{customerId}");

            if (response.IsSuccessStatusCode)
            {
                var customer = await response.Content.ReadFromJsonAsync<CustomerDto>();
                return customer;
            }

            _logger.LogWarning("Customer not found: CustomerId={CustomerId}, Status={Status}", customerId, response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching customer: CustomerId={CustomerId}", customerId);
            return null;
        }
    }
}

public record CustomerDto(
    Guid CustomerId,
    string FirstName,
    string LastName,
    string? Email,
    string? PhoneNumber
);

public interface IOrderServiceClient
{
    Task<OrderDetailsDto?> GetOrderDetailsAsync(Guid orderId);
}

public class OrderServiceClient : IOrderServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrderServiceClient> _logger;

    public OrderServiceClient(HttpClient httpClient, IConfiguration configuration, ILogger<OrderServiceClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<OrderDetailsDto?> GetOrderDetailsAsync(Guid orderId)
    {
        try
        {
            var apiKey = _configuration["Services:OrderService:InternalApiKey"];
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/order/internal/{orderId}/details");
            if (!string.IsNullOrEmpty(apiKey))
                request.Headers.Add("X-Internal-Api-Key", apiKey);

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<OrderDetailsDto>();

            _logger.LogWarning("Failed to fetch order details: OrderId={OrderId}, Status={Status}", orderId, response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception fetching order details: OrderId={OrderId}", orderId);
            return null;
        }
    }
}

public record OrderDetailsDto(
    Guid OrderId,
    Guid CustomerId,
    Guid RestaurantId,
    string Status,
    decimal Subtotal,
    decimal Tax,
    decimal DeliveryFee,
    decimal ServiceFee,
    decimal Total,
    string? DeliveryAddress,
    string? SpecialInstructions,
    DateTime CreatedAt,
    DateTime? DeliveredAt,
    List<OrderItemDto> Items
);

public record OrderItemDto(
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

public interface IRestaurantServiceClient
{
    Task<RestaurantInfoDto?> GetRestaurantAsync(Guid restaurantId);
}

public class RestaurantServiceClient : IRestaurantServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RestaurantServiceClient> _logger;

    public RestaurantServiceClient(HttpClient httpClient, ILogger<RestaurantServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<RestaurantInfoDto?> GetRestaurantAsync(Guid restaurantId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/restaurant/{restaurantId}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<RestaurantInfoDto>();

            _logger.LogWarning("Restaurant not found: {RestaurantId}, Status={Status}", restaurantId, response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch restaurant: {RestaurantId}", restaurantId);
            return null;
        }
    }
}

public record RestaurantInfoDto(
    Guid RestaurantId,
    string Name,
    string? Address,
    string? PhoneNumber
);
