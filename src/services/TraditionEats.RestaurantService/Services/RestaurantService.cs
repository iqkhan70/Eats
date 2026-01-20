using Microsoft.EntityFrameworkCore;
using TraditionEats.BuildingBlocks.Redis;
using TraditionEats.RestaurantService.Data;
using TraditionEats.RestaurantService.Entities;

namespace TraditionEats.RestaurantService.Services;

public interface IRestaurantService
{
    Task<Guid> CreateRestaurantAsync(Guid ownerId, CreateRestaurantDto dto);
    Task<RestaurantDto?> GetRestaurantAsync(Guid restaurantId);
    Task<List<RestaurantDto>> GetRestaurantsAsync(double? latitude, double? longitude, int skip = 0, int take = 20);
    Task<bool> UpdateRestaurantAsync(Guid restaurantId, Guid ownerId, UpdateRestaurantDto dto);
    Task<Guid> AddDeliveryZoneAsync(Guid restaurantId, Guid ownerId, CreateDeliveryZoneDto dto);
    Task<List<DeliveryZoneDto>> GetDeliveryZonesAsync(Guid restaurantId);
    Task<bool> SetRestaurantHoursAsync(Guid restaurantId, Guid ownerId, List<RestaurantHoursDto> hours);
    Task<List<RestaurantHoursDto>> GetRestaurantHoursAsync(Guid restaurantId);
    Task<bool> IsRestaurantOpenAsync(Guid restaurantId);
}

public class RestaurantService : IRestaurantService
{
    private readonly RestaurantDbContext _context;
    private readonly IRedisService _redis;
    private readonly ILogger<RestaurantService> _logger;

    public RestaurantService(
        RestaurantDbContext context,
        IRedisService redis,
        ILogger<RestaurantService> logger)
    {
        _context = context;
        _redis = redis;
        _logger = logger;
    }

    public async Task<Guid> CreateRestaurantAsync(Guid ownerId, CreateRestaurantDto dto)
    {
        var restaurantId = Guid.NewGuid();

        var restaurant = new Restaurant
        {
            RestaurantId = restaurantId,
            OwnerId = ownerId,
            Name = dto.Name,
            Description = dto.Description,
            CuisineType = dto.CuisineType,
            ImageUrl = dto.ImageUrl,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            Address = dto.Address,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Restaurants.Add(restaurant);
        await _context.SaveChangesAsync();

        // Cache restaurant for quick lookup
        await _redis.SetAsync($"restaurant:{restaurantId}", restaurant, TimeSpan.FromHours(1));

        _logger.LogInformation("Created restaurant {RestaurantId} for owner {OwnerId}", restaurantId, ownerId);
        return restaurantId;
    }

    public async Task<RestaurantDto?> GetRestaurantAsync(Guid restaurantId)
    {
        // Try cache first
        var cached = await _redis.GetAsync<RestaurantDto>($"restaurant:dto:{restaurantId}");
        if (cached != null)
        {
            return cached;
        }

        var restaurant = await _context.Restaurants
            .Include(r => r.DeliveryZones)
            .Include(r => r.Hours)
            .FirstOrDefaultAsync(r => r.RestaurantId == restaurantId);

        if (restaurant == null)
        {
            return null;
        }

        var dto = MapToDto(restaurant);
        
        // Cache for 1 hour
        await _redis.SetAsync($"restaurant:dto:{restaurantId}", dto, TimeSpan.FromHours(1));

        return dto;
    }

    public async Task<List<RestaurantDto>> GetRestaurantsAsync(double? latitude, double? longitude, int skip = 0, int take = 20)
    {
        var query = _context.Restaurants
            .Where(r => r.IsActive)
            .Include(r => r.DeliveryZones)
            .Include(r => r.Hours)
            .AsQueryable();

        // If coordinates provided, order by distance (simple approximation)
        if (latitude.HasValue && longitude.HasValue)
        {
            query = query.OrderBy(r => 
                Math.Abs(r.Latitude - latitude.Value) + Math.Abs(r.Longitude - longitude.Value));
        }

        var restaurants = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return restaurants.Select(MapToDto).ToList();
    }

    public async Task<bool> UpdateRestaurantAsync(Guid restaurantId, Guid ownerId, UpdateRestaurantDto dto)
    {
        var restaurant = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.RestaurantId == restaurantId && r.OwnerId == ownerId);

        if (restaurant == null)
        {
            return false;
        }

        if (dto.Name != null) restaurant.Name = dto.Name;
        if (dto.Description != null) restaurant.Description = dto.Description;
        if (dto.CuisineType != null) restaurant.CuisineType = dto.CuisineType;
        if (dto.ImageUrl != null) restaurant.ImageUrl = dto.ImageUrl;
        if (dto.PhoneNumber != null) restaurant.PhoneNumber = dto.PhoneNumber;
        if (dto.Email != null) restaurant.Email = dto.Email;
        if (dto.Address != null) restaurant.Address = dto.Address;
        if (dto.Latitude.HasValue) restaurant.Latitude = dto.Latitude.Value;
        if (dto.Longitude.HasValue) restaurant.Longitude = dto.Longitude.Value;
        if (dto.IsActive.HasValue) restaurant.IsActive = dto.IsActive.Value;

        restaurant.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Invalidate cache
        await _redis.DeleteAsync($"restaurant:{restaurantId}");
        await _redis.DeleteAsync($"restaurant:dto:{restaurantId}");

        return true;
    }

    public async Task<Guid> AddDeliveryZoneAsync(Guid restaurantId, Guid ownerId, CreateDeliveryZoneDto dto)
    {
        var restaurant = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.RestaurantId == restaurantId && r.OwnerId == ownerId);

        if (restaurant == null)
        {
            throw new UnauthorizedAccessException("Restaurant not found or you don't own it");
        }

        var zoneId = Guid.NewGuid();
        var zone = new DeliveryZone
        {
            ZoneId = zoneId,
            RestaurantId = restaurantId,
            Name = dto.Name,
            PolygonCoordinatesJson = System.Text.Json.JsonSerializer.Serialize(dto.Coordinates),
            DeliveryFee = dto.DeliveryFee,
            EstimatedMinutes = dto.EstimatedMinutes,
            MinimumOrderAmount = dto.MinimumOrderAmount,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.DeliveryZones.Add(zone);
        await _context.SaveChangesAsync();

        // Invalidate restaurant cache
        await _redis.DeleteAsync($"restaurant:dto:{restaurantId}");

        return zoneId;
    }

    public async Task<List<DeliveryZoneDto>> GetDeliveryZonesAsync(Guid restaurantId)
    {
        var zones = await _context.DeliveryZones
            .Where(z => z.RestaurantId == restaurantId && z.IsActive)
            .ToListAsync();

        return zones.Select(z => new DeliveryZoneDto
        {
            ZoneId = z.ZoneId,
            Name = z.Name,
            Coordinates = System.Text.Json.JsonSerializer.Deserialize<List<CoordinateDto>>(z.PolygonCoordinatesJson ?? "[]") ?? new(),
            DeliveryFee = z.DeliveryFee,
            EstimatedMinutes = z.EstimatedMinutes,
            MinimumOrderAmount = z.MinimumOrderAmount
        }).ToList();
    }

    public async Task<bool> SetRestaurantHoursAsync(Guid restaurantId, Guid ownerId, List<RestaurantHoursDto> hours)
    {
        var restaurant = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.RestaurantId == restaurantId && r.OwnerId == ownerId);

        if (restaurant == null)
        {
            return false;
        }

        // Remove existing hours
        var existingHours = await _context.RestaurantHours
            .Where(h => h.RestaurantId == restaurantId)
            .ToListAsync();
        _context.RestaurantHours.RemoveRange(existingHours);

        // Add new hours
        foreach (var hourDto in hours)
        {
            var hour = new RestaurantHours
            {
                HoursId = Guid.NewGuid(),
                RestaurantId = restaurantId,
                DayOfWeek = hourDto.DayOfWeek,
                OpenTime = hourDto.OpenTime,
                CloseTime = hourDto.CloseTime,
                IsClosed = hourDto.IsClosed
            };
            _context.RestaurantHours.Add(hour);
        }

        await _context.SaveChangesAsync();

        // Invalidate cache
        await _redis.DeleteAsync($"restaurant:dto:{restaurantId}");

        return true;
    }

    public async Task<List<RestaurantHoursDto>> GetRestaurantHoursAsync(Guid restaurantId)
    {
        var hours = await _context.RestaurantHours
            .Where(h => h.RestaurantId == restaurantId)
            .OrderBy(h => h.DayOfWeek)
            .ToListAsync();

        return hours.Select(h => new RestaurantHoursDto
        {
            DayOfWeek = h.DayOfWeek,
            OpenTime = h.OpenTime,
            CloseTime = h.CloseTime,
            IsClosed = h.IsClosed
        }).ToList();
    }

    public async Task<bool> IsRestaurantOpenAsync(Guid restaurantId)
    {
        var now = DateTime.UtcNow;
        var dayOfWeek = now.DayOfWeek;
        var currentTime = TimeOnly.FromDateTime(now);

        var hours = await _context.RestaurantHours
            .FirstOrDefaultAsync(h => h.RestaurantId == restaurantId && h.DayOfWeek == dayOfWeek);

        if (hours == null || hours.IsClosed)
        {
            return false;
        }

        if (hours.OpenTime == null || hours.CloseTime == null)
        {
            return false;
        }

        return currentTime >= hours.OpenTime && currentTime <= hours.CloseTime;
    }

    private RestaurantDto MapToDto(Restaurant restaurant)
    {
        return new RestaurantDto
        {
            RestaurantId = restaurant.RestaurantId,
            OwnerId = restaurant.OwnerId,
            Name = restaurant.Name,
            Description = restaurant.Description,
            CuisineType = restaurant.CuisineType,
            ImageUrl = restaurant.ImageUrl,
            PhoneNumber = restaurant.PhoneNumber,
            Email = restaurant.Email,
            Address = restaurant.Address,
            Latitude = restaurant.Latitude,
            Longitude = restaurant.Longitude,
            IsActive = restaurant.IsActive,
            Rating = restaurant.Rating,
            ReviewCount = restaurant.ReviewCount,
            CreatedAt = restaurant.CreatedAt,
            UpdatedAt = restaurant.UpdatedAt,
            DeliveryZones = restaurant.DeliveryZones.Select(z => new DeliveryZoneDto
            {
                ZoneId = z.ZoneId,
                Name = z.Name,
                Coordinates = System.Text.Json.JsonSerializer.Deserialize<List<CoordinateDto>>(z.PolygonCoordinatesJson ?? "[]") ?? new(),
                DeliveryFee = z.DeliveryFee,
                EstimatedMinutes = z.EstimatedMinutes,
                MinimumOrderAmount = z.MinimumOrderAmount
            }).ToList(),
            Hours = restaurant.Hours.Select(h => new RestaurantHoursDto
            {
                DayOfWeek = h.DayOfWeek,
                OpenTime = h.OpenTime,
                CloseTime = h.CloseTime,
                IsClosed = h.IsClosed
            }).ToList()
        };
    }
}

// DTOs
public record CreateRestaurantDto(
    string Name,
    string? Description,
    string? CuisineType,
    string? ImageUrl,
    string? PhoneNumber,
    string? Email,
    string Address,
    double Latitude,
    double Longitude);

public record UpdateRestaurantDto(
    string? Name,
    string? Description,
    string? CuisineType,
    string? ImageUrl,
    string? PhoneNumber,
    string? Email,
    string? Address,
    double? Latitude,
    double? Longitude,
    bool? IsActive);

public record RestaurantDto
{
    public Guid RestaurantId { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CuisineType { get; set; }
    public string? ImageUrl { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsActive { get; set; }
    public decimal? Rating { get; set; }
    public int? ReviewCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<DeliveryZoneDto> DeliveryZones { get; set; } = new();
    public List<RestaurantHoursDto> Hours { get; set; } = new();
}

public record CreateDeliveryZoneDto(
    string Name,
    List<CoordinateDto> Coordinates,
    decimal DeliveryFee,
    int EstimatedMinutes,
    decimal? MinimumOrderAmount);

public record DeliveryZoneDto
{
    public Guid ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<CoordinateDto> Coordinates { get; set; } = new();
    public decimal DeliveryFee { get; set; }
    public int EstimatedMinutes { get; set; }
    public decimal? MinimumOrderAmount { get; set; }
}

public record CoordinateDto(double Latitude, double Longitude);

public record RestaurantHoursDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
    public bool IsClosed { get; set; }
}
