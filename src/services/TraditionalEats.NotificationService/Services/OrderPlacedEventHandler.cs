using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TraditionalEats.Contracts.Events;

namespace TraditionalEats.NotificationService.Services;

public class OrderPlacedEventHandler : BackgroundService
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderPlacedEventHandler> _logger;
    private readonly ConnectionFactory _factory;
    private readonly IConfiguration _configuration;

    public OrderPlacedEventHandler(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<OrderPlacedEventHandler> logger)
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

        TryConnect();
    }

    private void TryConnect()
    {
        try
        {
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare("tradition-eats", ExchangeType.Topic, durable: true);
            var queueName = _channel.QueueDeclare("notification-service-order-placed", durable: true, exclusive: false, autoDelete: false).QueueName;
            _channel.QueueBind(queueName, "tradition-eats", "order.placed", null);
            _channel.BasicQos(0, 1, false);

            _logger.LogInformation("RabbitMQ connection established for NotificationService OrderPlacedEventHandler");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to RabbitMQ for order placed notifications");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_channel == null || _connection == null || !_connection.IsOpen)
            {
                await Task.Delay(30000, stoppingToken);
                TryConnect();
                continue;
            }

            try
            {
                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (_, ea) =>
                {
                    if (_channel == null) return;

                    var message = Encoding.UTF8.GetString(ea.Body.ToArray());

                    try
                    {
                        var evt = JsonSerializer.Deserialize<OrderPlacedEvent>(message);
                        if (evt != null)
                        {
                            await HandleOrderPlacedAsync(evt, stoppingToken);
                        }

                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing order placed notification event");
                        if (_channel.IsOpen)
                            _channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                };

                _channel.BasicConsume("notification-service-order-placed", false, consumer);

                while (!stoppingToken.IsCancellationRequested &&
                       _connection?.IsOpen == true &&
                       _channel?.IsOpen == true)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OrderPlacedEventHandler failed; retrying");
                _connection?.Dispose();
                _channel?.Dispose();
                _connection = null;
                _channel = null;
                await Task.Delay(30000, stoppingToken);
            }
        }
    }

    private async Task HandleOrderPlacedAsync(OrderPlacedEvent evt, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var db = scope.ServiceProvider.GetRequiredService<Data.NotificationDbContext>();

        try
        {
            var restaurantClient = httpClientFactory.CreateClient("RestaurantService");
            var response = await restaurantClient.GetAsync($"/api/restaurant/{evt.RestaurantId}/notification-recipients", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Skipping push for order {OrderId}: failed to resolve restaurant recipients ({StatusCode})", evt.OrderId, response.StatusCode);
                return;
            }

            var recipients = await response.Content.ReadFromJsonAsync<RestaurantNotificationRecipientsDto>(cancellationToken: cancellationToken);
            if (recipients == null)
                return;

            var userIds = new List<Guid> { recipients.OwnerUserId };
            userIds.AddRange(recipients.StaffUserIds ?? []);

            var title = "New order received";
            var orderShort = evt.OrderId.ToString()[..8];
            var body = $"{recipients.RestaurantName}: Order #{orderShort} for ${evt.TotalAmount:F2}";
            var data = new Dictionary<string, string>
            {
                ["url"] = $"/vendor/orders/{evt.OrderId}?restaurantId={evt.RestaurantId}&source=notification",
                ["orderId"] = evt.OrderId.ToString(),
                ["restaurantId"] = evt.RestaurantId.ToString(),
                ["type"] = "order_placed"
            };

            var sentCount = await notificationService.SendPushToUsersAsync(
                userIds,
                "order_placed",
                title,
                body,
                data);

            _logger.LogInformation(
                "Sent {Count} order placed push notifications for OrderId={OrderId}, RestaurantId={RestaurantId}",
                sentCount,
                evt.OrderId,
                evt.RestaurantId);

            var reminderIntervalMinutes = _configuration.GetValue<int?>("Notifications:OrderReminderIntervalMinutes") ?? 10;
            var reminderMaxCount = _configuration.GetValue<int?>("Notifications:OrderReminderMaxCount") ?? 5;

            var schedule = await db.OrderReminderSchedules.FirstOrDefaultAsync(r => r.OrderId == evt.OrderId, cancellationToken);
            if (schedule == null)
            {
                schedule = new Entities.OrderReminderSchedule
                {
                    OrderReminderScheduleId = Guid.NewGuid(),
                    OrderId = evt.OrderId,
                    RestaurantId = evt.RestaurantId,
                    ReminderCountSent = 0,
                    MaxReminders = reminderMaxCount,
                    IntervalMinutes = reminderIntervalMinutes,
                    NextReminderAt = DateTime.UtcNow.AddMinutes(reminderIntervalMinutes),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.OrderReminderSchedules.Add(schedule);
            }
            else
            {
                schedule.RestaurantId = evt.RestaurantId;
                schedule.ReminderCountSent = 0;
                schedule.MaxReminders = reminderMaxCount;
                schedule.IntervalMinutes = reminderIntervalMinutes;
                schedule.NextReminderAt = DateTime.UtcNow.AddMinutes(reminderIntervalMinutes);
                schedule.IsActive = true;
                schedule.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order placed push notifications for OrderId={OrderId}", evt.OrderId);
        }
    }

    public override void Dispose()
    {
        try
        {
            if (_channel?.IsOpen == true)
                _channel.Close();
            if (_connection?.IsOpen == true)
                _connection.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing NotificationService order placed event handler");
        }
        finally
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }

    private sealed record RestaurantNotificationRecipientsDto
    {
        public Guid RestaurantId { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public Guid OwnerUserId { get; set; }
        public List<Guid> StaffUserIds { get; set; } = [];
    }
}
