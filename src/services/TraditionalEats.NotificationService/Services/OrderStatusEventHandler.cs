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
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderStatusEventHandler> _logger;
    private readonly IConfiguration _configuration;

    public OrderStatusEventHandler(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<OrderStatusEventHandler> logger)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:UserName"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare exchange and queue
        _channel.ExchangeDeclare("tradition-eats", ExchangeType.Topic, durable: true);
        var queueName = _channel.QueueDeclare("notification-service-order-status", durable: true, exclusive: false, autoDelete: false).QueueName;
        _channel.QueueBind(queueName, "tradition-eats", "order.status.changed", null);
        _channel.BasicQos(0, 1, false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
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
                _channel.BasicNack(ea.DeliveryTag, false, true); // Requeue on error
            }
        };

        _channel.BasicConsume("notification-service-order-status", false, consumer);

        _logger.LogInformation("OrderStatusEventHandler started, waiting for messages...");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
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
    <p>Thank you for choosing TraditionalEats!</p>
</body>
</html>";

            var smsBody = $"Your order #{evt.OrderId.ToString().Substring(0, 8)} is ready for pickup! Please come to the restaurant to collect it. - TraditionalEats";

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
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
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
