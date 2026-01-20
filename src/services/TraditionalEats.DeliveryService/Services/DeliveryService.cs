using Microsoft.EntityFrameworkCore;
using TraditionalEats.BuildingBlocks.Redis;
using TraditionalEats.BuildingBlocks.Messaging;
using TraditionalEats.DeliveryService.Data;
using TraditionalEats.DeliveryService.Entities;

namespace TraditionalEats.DeliveryService.Services;

public interface IDeliveryService
{
    Task<Guid> RegisterDriverAsync(Guid userId, RegisterDriverDto dto);
    Task<DriverDto?> GetDriverAsync(Guid driverId);
    Task<DriverDto?> GetDriverByUserIdAsync(Guid userId);
    Task<bool> UpdateDriverAvailabilityAsync(Guid driverId, bool isAvailable);
    Task<bool> UpdateDriverLocationAsync(Guid driverId, double latitude, double longitude);
    Task<Guid> CreateDeliveryAsync(Guid orderId, CreateDeliveryDto dto);
    Task<DeliveryDto?> GetDeliveryAsync(Guid deliveryId);
    Task<DeliveryDto?> GetDeliveryByOrderIdAsync(Guid orderId);
    Task<bool> AssignDriverAsync(Guid deliveryId, Guid driverId);
    Task<bool> UpdateDeliveryStatusAsync(Guid deliveryId, string status, double? latitude = null, double? longitude = null);
    Task<List<DriverDto>> GetAvailableDriversAsync(double? latitude, double? longitude);
    Task<List<DeliveryTrackingDto>> GetDeliveryTrackingAsync(Guid deliveryId);
}

public class DeliveryService : IDeliveryService
{
    private readonly DeliveryDbContext _context;
    private readonly IRedisService _redis;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<DeliveryService> _logger;

    public DeliveryService(
        DeliveryDbContext context,
        IRedisService redis,
        IMessagePublisher messagePublisher,
        ILogger<DeliveryService> logger)
    {
        _context = context;
        _redis = redis;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    public async Task<Guid> RegisterDriverAsync(Guid userId, RegisterDriverDto dto)
    {
        var driverId = Guid.NewGuid();

        var driver = new Driver
        {
            DriverId = driverId,
            UserId = userId,
            Name = dto.Name,
            PhoneNumber = dto.PhoneNumber,
            VehicleType = dto.VehicleType,
            VehicleLicensePlate = dto.VehicleLicensePlate,
            IsAvailable = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Drivers.Add(driver);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Registered driver {DriverId} for user {UserId}", driverId, userId);
        return driverId;
    }

    public async Task<DriverDto?> GetDriverAsync(Guid driverId)
    {
        var driver = await _context.Drivers
            .FirstOrDefaultAsync(d => d.DriverId == driverId);

        return driver == null ? null : MapToDto(driver);
    }

    public async Task<DriverDto?> GetDriverByUserIdAsync(Guid userId)
    {
        var driver = await _context.Drivers
            .FirstOrDefaultAsync(d => d.UserId == userId);

        return driver == null ? null : MapToDto(driver);
    }

    public async Task<bool> UpdateDriverAvailabilityAsync(Guid driverId, bool isAvailable)
    {
        var driver = await _context.Drivers
            .FirstOrDefaultAsync(d => d.DriverId == driverId);

        if (driver == null)
        {
            return false;
        }

        driver.IsAvailable = isAvailable;
        driver.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Update Redis for quick lookup
        if (isAvailable)
        {
            await _redis.SetAsync($"driver:available:{driverId}", true, TimeSpan.FromHours(1));
        }
        else
        {
            await _redis.DeleteAsync($"driver:available:{driverId}");
        }

        return true;
    }

    public async Task<bool> UpdateDriverLocationAsync(Guid driverId, double latitude, double longitude)
    {
        var driver = await _context.Drivers
            .FirstOrDefaultAsync(d => d.DriverId == driverId);

        if (driver == null)
        {
            return false;
        }

        driver.CurrentLatitude = latitude;
        driver.CurrentLongitude = longitude;
        driver.LastLocationUpdate = DateTime.UtcNow;
        driver.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Store in Redis for real-time tracking
        await _redis.SetAsync($"driver:location:{driverId}", new { latitude, longitude }, TimeSpan.FromMinutes(5));

        return true;
    }

    public async Task<Guid> CreateDeliveryAsync(Guid orderId, CreateDeliveryDto dto)
    {
        var deliveryId = Guid.NewGuid();

        var delivery = new Delivery
        {
            DeliveryId = deliveryId,
            OrderId = orderId,
            Status = "Pending",
            PickupAddress = dto.PickupAddress,
            DeliveryAddress = dto.DeliveryAddress,
            PickupLatitude = dto.PickupLatitude,
            PickupLongitude = dto.PickupLongitude,
            DeliveryLatitude = dto.DeliveryLatitude,
            DeliveryLongitude = dto.DeliveryLongitude,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Deliveries.Add(delivery);
        await _context.SaveChangesAsync();

        // Publish event
        await _messagePublisher.PublishAsync("", "delivery.created", new
        {
            DeliveryId = deliveryId,
            OrderId = orderId,
            Status = "Pending"
        });

        _logger.LogInformation("Created delivery {DeliveryId} for order {OrderId}", deliveryId, orderId);
        return deliveryId;
    }

    public async Task<DeliveryDto?> GetDeliveryAsync(Guid deliveryId)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Driver)
            .Include(d => d.TrackingHistory)
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId);

        return delivery == null ? null : MapToDto(delivery);
    }

    public async Task<DeliveryDto?> GetDeliveryByOrderIdAsync(Guid orderId)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Driver)
            .Include(d => d.TrackingHistory)
            .FirstOrDefaultAsync(d => d.OrderId == orderId);

        return delivery == null ? null : MapToDto(delivery);
    }

    public async Task<bool> AssignDriverAsync(Guid deliveryId, Guid driverId)
    {
        var delivery = await _context.Deliveries
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId);

        var driver = await _context.Drivers
            .FirstOrDefaultAsync(d => d.DriverId == driverId && d.IsAvailable);

        if (delivery == null || driver == null)
        {
            return false;
        }

        delivery.DriverId = driverId;
        delivery.Status = "Assigned";
        delivery.AssignedAt = DateTime.UtcNow;
        delivery.UpdatedAt = DateTime.UtcNow;

        driver.IsAvailable = false;
        driver.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Add tracking entry
        await AddTrackingEntryAsync(deliveryId, "Assigned", delivery.DeliveryLatitude, delivery.DeliveryLongitude, $"Assigned to driver {driver.Name}");

        // Publish event
        await _messagePublisher.PublishAsync("", "delivery.assigned", new
        {
            DeliveryId = deliveryId,
            OrderId = delivery.OrderId,
            DriverId = driverId
        });

        return true;
    }

    public async Task<bool> UpdateDeliveryStatusAsync(Guid deliveryId, string status, double? latitude = null, double? longitude = null)
    {
        var delivery = await _context.Deliveries
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId);

        if (delivery == null)
        {
            return false;
        }

        var oldStatus = delivery.Status;
        delivery.Status = status;
        delivery.UpdatedAt = DateTime.UtcNow;

        // Update timestamps based on status
        switch (status.ToLower())
        {
            case "pickedup":
                delivery.PickedUpAt = DateTime.UtcNow;
                break;
            case "delivered":
                delivery.DeliveredAt = DateTime.UtcNow;
                if (delivery.DriverId.HasValue)
                {
                    var driver = await _context.Drivers.FindAsync(delivery.DriverId.Value);
                    if (driver != null)
                    {
                        driver.IsAvailable = true;
                    }
                }
                break;
        }

        await _context.SaveChangesAsync();

        // Add tracking entry
        await AddTrackingEntryAsync(deliveryId, status, latitude, longitude);

        // Publish event
        await _messagePublisher.PublishAsync("", "delivery.status.updated", new
        {
            DeliveryId = deliveryId,
            OrderId = delivery.OrderId,
            OldStatus = oldStatus,
            NewStatus = status
        });

        return true;
    }

    public async Task<List<DriverDto>> GetAvailableDriversAsync(double? latitude, double? longitude)
    {
        var query = _context.Drivers
            .Where(d => d.IsAvailable && d.IsActive)
            .AsQueryable();

        // If coordinates provided, order by distance
        if (latitude.HasValue && longitude.HasValue)
        {
            query = query.OrderBy(d => 
                Math.Abs((d.CurrentLatitude ?? 0) - latitude.Value) + 
                Math.Abs((d.CurrentLongitude ?? 0) - longitude.Value));
        }

        var drivers = await query.ToListAsync();
        return drivers.Select(MapToDto).ToList();
    }

    public async Task<List<DeliveryTrackingDto>> GetDeliveryTrackingAsync(Guid deliveryId)
    {
        var tracking = await _context.DeliveryTracking
            .Where(t => t.DeliveryId == deliveryId)
            .OrderBy(t => t.Timestamp)
            .ToListAsync();

        return tracking.Select(t => new DeliveryTrackingDto
        {
            TrackingId = t.TrackingId,
            Latitude = t.Latitude,
            Longitude = t.Longitude,
            Status = t.Status,
            Timestamp = t.Timestamp,
            Notes = t.Notes
        }).ToList();
    }

    private async Task AddTrackingEntryAsync(Guid deliveryId, string status, double? latitude, double? longitude, string? notes = null)
    {
        var tracking = new DeliveryTracking
        {
            TrackingId = Guid.NewGuid(),
            DeliveryId = deliveryId,
            Status = status,
            Latitude = latitude ?? 0,
            Longitude = longitude ?? 0,
            Timestamp = DateTime.UtcNow,
            Notes = notes
        };

        _context.DeliveryTracking.Add(tracking);
        await _context.SaveChangesAsync();
    }

    private DriverDto MapToDto(Driver driver)
    {
        return new DriverDto
        {
            DriverId = driver.DriverId,
            UserId = driver.UserId,
            Name = driver.Name,
            PhoneNumber = driver.PhoneNumber,
            VehicleType = driver.VehicleType,
            VehicleLicensePlate = driver.VehicleLicensePlate,
            IsAvailable = driver.IsAvailable,
            IsActive = driver.IsActive,
            CurrentLatitude = driver.CurrentLatitude,
            CurrentLongitude = driver.CurrentLongitude,
            LastLocationUpdate = driver.LastLocationUpdate,
            CreatedAt = driver.CreatedAt,
            UpdatedAt = driver.UpdatedAt
        };
    }

    private DeliveryDto MapToDto(Delivery delivery)
    {
        return new DeliveryDto
        {
            DeliveryId = delivery.DeliveryId,
            OrderId = delivery.OrderId,
            DriverId = delivery.DriverId,
            DriverName = delivery.Driver?.Name,
            Status = delivery.Status,
            PickupAddress = delivery.PickupAddress,
            DeliveryAddress = delivery.DeliveryAddress,
            PickupLatitude = delivery.PickupLatitude,
            PickupLongitude = delivery.PickupLongitude,
            DeliveryLatitude = delivery.DeliveryLatitude,
            DeliveryLongitude = delivery.DeliveryLongitude,
            AssignedAt = delivery.AssignedAt,
            PickedUpAt = delivery.PickedUpAt,
            DeliveredAt = delivery.DeliveredAt,
            CreatedAt = delivery.CreatedAt,
            UpdatedAt = delivery.UpdatedAt,
            TrackingHistory = delivery.TrackingHistory.Select(t => new DeliveryTrackingDto
            {
                TrackingId = t.TrackingId,
                Latitude = t.Latitude,
                Longitude = t.Longitude,
                Status = t.Status,
                Timestamp = t.Timestamp,
                Notes = t.Notes
            }).ToList()
        };
    }
}

// DTOs
public record RegisterDriverDto(
    string Name,
    string PhoneNumber,
    string VehicleType,
    string VehicleLicensePlate);

public record DriverDto
{
    public Guid DriverId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string VehicleLicensePlate { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public bool IsActive { get; set; }
    public double? CurrentLatitude { get; set; }
    public double? CurrentLongitude { get; set; }
    public DateTime? LastLocationUpdate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public record CreateDeliveryDto(
    string PickupAddress,
    string DeliveryAddress,
    double? PickupLatitude,
    double? PickupLongitude,
    double? DeliveryLatitude,
    double? DeliveryLongitude);

public record DeliveryDto
{
    public Guid DeliveryId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PickupAddress { get; set; }
    public string? DeliveryAddress { get; set; }
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }
    public double? DeliveryLatitude { get; set; }
    public double? DeliveryLongitude { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<DeliveryTrackingDto> TrackingHistory { get; set; } = new();
}

public record DeliveryTrackingDto
{
    public Guid TrackingId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Notes { get; set; }
}
