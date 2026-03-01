using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TraditionalEats.ChatService.Hubs;
using TraditionalEats.Contracts.Events;

namespace TraditionalEats.ChatService.Services;

public class OrderPlacedEventHandler : BackgroundService
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderPlacedEventHandler> _logger;
    private readonly ConnectionFactory _factory;

    public OrderPlacedEventHandler(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<OrderPlacedEventHandler> logger)
    {
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
            var queueName = _channel.QueueDeclare("chat-service-order-placed", durable: true, exclusive: false, autoDelete: false).QueueName;
            _channel.QueueBind(queueName, "tradition-eats", "order.placed", null);
            _channel.BasicQos(0, 1, false);

            _logger.LogInformation("RabbitMQ connection established for OrderPlacedEventHandler");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to RabbitMQ. Order placed event handling will be disabled. HostName={HostName}, Port={Port}",
                _factory.HostName, _factory.Port);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                        _logger.LogInformation("Received message: {RoutingKey}", routingKey);

                        if (routingKey == "order.placed")
                        {
                            var evt = JsonSerializer.Deserialize<OrderPlacedEvent>(message);
                            if (evt != null)
                            {
                                await HandleOrderPlacedAsync(evt);
                            }
                        }

                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing order.placed message: {Message}", message);
                        if (_channel != null && _channel.IsOpen)
                        {
                            _channel.BasicNack(ea.DeliveryTag, false, true);
                        }
                    }
                };

                _channel.BasicConsume("chat-service-order-placed", false, consumer);
                _logger.LogInformation("OrderPlacedEventHandler started, waiting for messages...");

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
                _logger.LogError(ex, "Error in OrderPlacedEventHandler. Will retry...");
                _connection?.Dispose();
                _channel?.Dispose();
                _connection = null;
                _channel = null;
                await Task.Delay(30000, stoppingToken);
            }
        }
    }

    private async Task HandleOrderPlacedAsync(OrderPlacedEvent evt)
    {
        _logger.LogInformation("Handling order placed: OrderId={OrderId}, CustomerId={CustomerId}, RestaurantId={RestaurantId}",
            evt.OrderId, evt.CustomerId, evt.RestaurantId);

        using var scope = _serviceProvider.CreateScope();
        var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<VendorChatHub>>();

        try
        {
            var conversation = await chatService.GetOrCreateVendorConversationAsync(evt.RestaurantId, evt.CustomerId, null);

            var metadata = new
            {
                type = "order_placed",
                orderId = evt.OrderId,
                items = evt.Items.Select(i => new
                {
                    menuItemId = i.MenuItemId,
                    name = i.Name,
                    quantity = i.Quantity,
                    unitPrice = i.UnitPrice,
                    totalPrice = i.TotalPrice,
                    modifiers = i.Modifiers ?? new List<string>()
                }).ToList(),
                total = evt.TotalAmount,
                serviceFee = evt.ServiceFee,
                deliveryAddress = evt.DeliveryAddress,
                placedAt = evt.PlacedAt.ToString("O")
            };

            var metadataJson = JsonSerializer.Serialize(metadata);

            var saved = await chatService.SaveVendorMessageAsync(
                conversation.ConversationId,
                Guid.Empty,
                "System",
                null,
                "Order placed",
                metadataJson);

            var groupName = $"vendor_chat_{conversation.ConversationId}";
            await hubContext.Clients.Group(groupName).SendAsync("ReceiveVendorMessage", new
            {
                MessageId = saved.MessageId,
                ConversationId = saved.ConversationId,
                SenderId = saved.SenderId,
                SenderRole = saved.SenderRole,
                SenderDisplayName = saved.SenderDisplayName,
                Message = saved.Message,
                SentAt = saved.SentAt,
                MetadataJson = saved.MetadataJson
            });

            _logger.LogInformation("Order placed message added to vendor chat: ConversationId={ConversationId}, MessageId={MessageId}",
                conversation.ConversationId, saved.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling order placed event: OrderId={OrderId}", evt.OrderId);
        }
    }

    public override void Dispose()
    {
        try
        {
            if (_channel != null && _channel.IsOpen)
                _channel.Close();
            if (_connection != null && _connection.IsOpen)
                _connection.Close();
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
