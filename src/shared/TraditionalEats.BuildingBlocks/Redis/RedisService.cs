using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace TraditionalEats.BuildingBlocks.Redis;

public interface IRedisService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task<bool> DeleteAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task<bool> AcquireLockAsync(string key, TimeSpan expiry);
    Task ReleaseLockAsync(string key);
    Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiry);
    Task<long> IncrementAsync(string key, TimeSpan? expiry = null);
    Task<bool> AddToSetAsync(string key, string value);
    Task<bool> IsInSetAsync(string key, string value);
}

public class RedisService : IRedisService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisService> _logger;

    public RedisService(IConnectionMultiplexer redis, ILogger<RedisService> logger)
    {
        _redis = redis;
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            if (!value.HasValue)
                return default;

            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get key {Key} from Redis", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, json, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set key {Key} in Redis", key);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string key)
    {
        try
        {
            return await _database.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete key {Key} from Redis", key);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence of key {Key} in Redis", key);
            return false;
        }
    }

    public async Task<bool> AcquireLockAsync(string key, TimeSpan expiry)
    {
        try
        {
            var lockKey = $"lock:{key}";
            return await _database.StringSetAsync(lockKey, "locked", expiry, When.NotExists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire lock for key {Key}", key);
            return false;
        }
    }

    public async Task ReleaseLockAsync(string key)
    {
        try
        {
            var lockKey = $"lock:{key}";
            await _database.KeyDeleteAsync(lockKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release lock for key {Key}", key);
        }
    }

    public async Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiry)
    {
        try
        {
            return await _database.StringSetAsync(key, value, expiry, When.NotExists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set if not exists for key {Key}", key);
            return false;
        }
    }

    public async Task<long> IncrementAsync(string key, TimeSpan? expiry = null)
    {
        try
        {
            var value = await _database.StringIncrementAsync(key);
            if (expiry.HasValue && value == 1)
            {
                await _database.KeyExpireAsync(key, expiry.Value);
            }
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment key {Key}", key);
            throw;
        }
    }

    public async Task<bool> AddToSetAsync(string key, string value)
    {
        try
        {
            return await _database.SetAddAsync(key, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add to set {Key}", key);
            return false;
        }
    }

    public async Task<bool> IsInSetAsync(string key, string value)
    {
        try
        {
            return await _database.SetContainsAsync(key, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check set membership for key {Key}", key);
            return false;
        }
    }
}

public static class RedisExtensions
{
    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        // Try multiple configuration paths
        var connectionString = configuration.GetConnectionString("Redis")
            ?? configuration["Redis:ConnectionString"]
            ?? configuration["Redis"]
            ?? "localhost:6379"; // Default fallback

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            return ConnectionMultiplexer.Connect(connectionString);
        });

        services.AddScoped<IRedisService, RedisService>();

        return services;
    }
}
