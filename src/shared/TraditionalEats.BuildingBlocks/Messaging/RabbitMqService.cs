using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace TraditionalEats.BuildingBlocks.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync<T>(string exchange, string routingKey, T message, CancellationToken cancellationToken = default);
}

public class RabbitMqService : IMessagePublisher, IDisposable
{
    private readonly IConnection? _connection;
    private readonly IModel? _channel;
    private readonly ILogger<RabbitMqService> _logger;
    private readonly bool _isConnected;

    public RabbitMqService(IConfiguration configuration, ILogger<RabbitMqService> logger)
    {
        _logger = logger;
        IConnection? connection = null;
        IModel? channel = null;
        
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
                Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = configuration["RabbitMQ:UserName"] ?? "guest",
                Password = configuration["RabbitMQ:Password"] ?? "guest"
            };

            connection = factory.CreateConnection();
            channel = connection.CreateModel();
            _isConnected = true;
            _logger.LogInformation("RabbitMQ connection established to {HostName}:{Port}", factory.HostName, factory.Port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to RabbitMQ. Message publishing will be disabled. HostName={HostName}, UserName={UserName}", 
                configuration["RabbitMQ:HostName"] ?? "localhost",
                configuration["RabbitMQ:UserName"] ?? "guest");
            _isConnected = false;
            // Don't throw - allow service to start without RabbitMQ
        }

        _connection = connection;
        _channel = channel;
    }

    public Task PublishAsync<T>(string exchange, string routingKey, T message, CancellationToken cancellationToken = default)
    {
        try
        {
            _channel.ExchangeDeclare(exchange, ExchangeType.Topic, durable: true);
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = Guid.NewGuid().ToString();
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _channel.BasicPublish(
                exchange: exchange,
                routingKey: routingKey,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Published message to {Exchange} with routing key {RoutingKey}", exchange, routingKey);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to {Exchange} with routing key {RoutingKey}", exchange, routingKey);
            return Task.FromException(ex);
        }
    }

    public void Dispose()
    {
        try
        {
            _channel?.Close();
            _connection?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing RabbitMQ connection");
        }
        finally
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}

public static class RabbitMqExtensions
{
    public static IServiceCollection AddRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMessagePublisher, RabbitMqService>();
        return services;
    }
}
