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
                await Task.Delay(30000, stoppingToken);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OrderStatusEventHandler. Will retry...");
                _connection?.Dispose();
                _channel?.Dispose();
                _connection = null;
                _channel = null;
                await Task.Delay(30000, stoppingToken);
            }
        }
    }

    private async Task HandleOrderStatusChangedAsync(OrderStatusChangedEvent evt)
    {
        _logger.LogInformation("Handling order status changed: OrderId={OrderId}, OldStatus={OldStatus}, NewStatus={NewStatus}",
            evt.OrderId, evt.OldStatus, evt.NewStatus);

        // Only send notifications when status changes to "Ready"
        if (evt.NewStatus != "Ready")
        {
            _logger.LogInformation("Status is not 'Ready', skipping notification. NewStatus={NewStatus}", evt.NewStatus);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();

        try
        {
            // Get customer information
            var customer = await customerService.GetCustomerAsync(evt.CustomerId);
            if (customer == null)
            {
                _logger.LogWarning("Customer not found: CustomerId={CustomerId}", evt.CustomerId);
                return;
            }

            // Prepare notification message
            var subject = "Your Order is Ready for Pickup!";
            var emailBody = $@"
<html>
<body>
    <h2>Your Order is Ready for Pickup!</h2>
    <p>Hello {customer.FirstName},</p>
    <p>Your order #{evt.OrderId.ToString().Substring(0, 8)} is now ready for pickup!</p>
    <p>Please come to the restaurant to collect your order.</p>
    <p>Thank you for choosing Kram!</p>
</body>
</html>";

            var smsBody = $"Your order #{evt.OrderId.ToString().Substring(0, 8)} is ready for pickup! Please come to the restaurant to collect it. - Kram";

            // Send email notification
            if (!string.IsNullOrEmpty(customer.Email))
            {
                var emailSent = await notificationService.SendNotificationAsync(new SendNotificationDto(
                    evt.CustomerId,
                    "email",
                    "OrderReady",
                    subject,
                    emailBody,
                    customer.Email,
                    null
                ));

                _logger.LogInformation("Email notification sent: OrderId={OrderId}, CustomerId={CustomerId}, Sent={Sent}",
                    evt.OrderId, evt.CustomerId, emailSent);
            }

            // Send SMS notification
            if (string.IsNullOrEmpty(customer.PhoneNumber))
            {
                _logger.LogWarning("Skipping SMS: customer has no phone number. OrderId={OrderId}, CustomerId={CustomerId}", evt.OrderId, evt.CustomerId);
            }
            else
            {
                var smsSent = await notificationService.SendNotificationAsync(new SendNotificationDto(
                    evt.CustomerId,
                    "sms",
                    "OrderReady",
                    "Order Ready",
                    smsBody,
                    customer.PhoneNumber,
                    null
                ));

                _logger.LogInformation("SMS notification sent: OrderId={OrderId}, CustomerId={CustomerId}, Sent={Sent}",
                    evt.OrderId, evt.CustomerId, smsSent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling order status changed event: OrderId={OrderId}", evt.OrderId);
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
