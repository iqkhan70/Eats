using Microsoft.EntityFrameworkCore;
using TraditionalEats.BuildingBlocks.Redis;
using TraditionalEats.BuildingBlocks.Geocoding;
using TraditionalEats.RestaurantService.Data;
using TraditionalEats.RestaurantService.Entities;

namespace TraditionalEats.RestaurantService.Services;

public interface IRestaurantService
{
    Task<Guid> CreateRestaurantAsync(Guid ownerId, CreateRestaurantDto dto);
    Task<RestaurantDto?> GetRestaurantAsync(Guid restaurantId);
    Task<List<RestaurantDto>> GetRestaurantsAsync(string? location, string? cuisineType, double? latitude, double? longitude, double? radiusMiles = null, string? zip = null, int skip = 0, int take = 20);
    Task<bool> UpdateRestaurantAsync(Guid restaurantId, Guid ownerId, UpdateRestaurantDto dto);
    Task<Guid> AddDeliveryZoneAsync(Guid restaurantId, Guid ownerId, CreateDeliveryZoneDto dto);
    Task<List<DeliveryZoneDto>> GetDeliveryZonesAsync(Guid restaurantId);
    Task<bool> SetRestaurantHoursAsync(Guid restaurantId, Guid ownerId, List<RestaurantHoursDto> hours);
    Task<List<RestaurantHoursDto>> GetRestaurantHoursAsync(Guid restaurantId);
    Task<bool> IsRestaurantOpenAsync(Guid restaurantId);
    Task<List<string>> GetSearchSuggestionsAsync(string query, int maxResults = 10);

    // Vendor endpoints
    Task<List<RestaurantDto>> GetVendorRestaurantsAsync(Guid vendorId);
    Task<bool> DeleteRestaurantAsync(Guid restaurantId, Guid vendorId);

    // Admin endpoints
    Task<List<RestaurantDto>> GetAllRestaurantsAsync(int skip = 0, int take = 100);
    Task<bool> AdminUpdateRestaurantAsync(Guid restaurantId, UpdateRestaurantDto dto);
    Task<bool> AdminDeleteRestaurantAsync(Guid restaurantId);
    Task<bool> AdminToggleRestaurantStatusAsync(Guid restaurantId, bool isActive);
}

public class RestaurantService : IRestaurantService
{
    private readonly RestaurantDbContext _context;
    private readonly IRedisService _redis;
    private readonly IGeocodingService _geocoding;
    private readonly ILogger<RestaurantService> _logger;

    public RestaurantService(
        RestaurantDbContext context,
        IRedisService redis,
        IGeocodingService geocoding,
        ILogger<RestaurantService> logger)
    {
        _context = context;
        _redis = redis;
        _geocoding = geocoding;
        _logger = logger;
    }

    public async Task<Guid> CreateRestaurantAsync(Guid ownerId, CreateRestaurantDto dto)
    {
        var restaurantId = Guid.NewGuid();

        // Geocode address if lat/long not provided
        double latitude = dto.Latitude ?? 0;
        double longitude = dto.Longitude ?? 0;

        if ((dto.Latitude == null || dto.Longitude == null) && !string.IsNullOrWhiteSpace(dto.Address))
        {
            var geocodeResult = await _geocoding.GeocodeAddressAsync(dto.Address);
            if (geocodeResult.HasValue)
            {
                latitude = geocodeResult.Value.Latitude;
                longitude = geocodeResult.Value.Longitude;
                _logger.LogInformation("Geocoded address '{Address}' to lat={Latitude}, lon={Longitude}",
                    dto.Address, latitude, longitude);
            }
            else
            {
                _logger.LogWarning("Failed to geocode address '{Address}'. Using default coordinates (0,0).", dto.Address);
            }
        }

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
            Latitude = latitude,
            Longitude = longitude,
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

    public async Task<List<RestaurantDto>> GetRestaurantsAsync(string? location, string? cuisineType, double? latitude, double? longitude, double? radiusMiles = null, string? zip = null, int skip = 0, int take = 20)
    {
        // List endpoint: no Include(DeliveryZones/Hours) — list view doesn't need them; reduces data and query cost
        var query = _context.Restaurants
            .Where(r => r.IsActive)
            .AsQueryable();

        // When searching by coordinates (ZIP/near me), use ONLY radius + cuisine — do NOT filter by location text.
        if (!latitude.HasValue || !longitude.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(location))
                query = query.Where(r => r.Address.Contains(location) || r.Name.Contains(location));
        }
        else
        {
            // Bounding-box filter in DB so we don't load all restaurants: ~1 deg lat ≈ 69 mi, 1 deg lon ≈ 69*cos(lat) mi
            var lat = latitude.Value;
            var lon = longitude.Value;
            var radiusMi = Math.Max(1, Math.Min(150, radiusMiles ?? 100));
            var buffer = 1.15; // small buffer so we don't miss edge cases
            var deltaLat = (radiusMi / 69.0) * buffer;
            var cosLat = Math.Cos(lat * Math.PI / 180.0);
            var deltaLon = cosLat > 0.01 ? (radiusMi / (69.0 * cosLat)) * buffer : deltaLat;
            query = query.Where(r =>
                r.Latitude >= lat - deltaLat && r.Latitude <= lat + deltaLat &&
                r.Longitude >= lon - deltaLon && r.Longitude <= lon + deltaLon);
        }

        // Filter by cuisine type
        if (!string.IsNullOrWhiteSpace(cuisineType))
        {
            query = query.Where(r => r.CuisineType != null && r.CuisineType.Contains(cuisineType));
        }

        var restaurants = await query.ToListAsync();

        // If coordinates provided, filter by exact Haversine radius and order by distance (in-memory on already-reduced set)
        if (latitude.HasValue && longitude.HasValue)
        {
            var lat = latitude.Value;
            var lon = longitude.Value;
            var radiusMi = Math.Max(1, Math.Min(150, radiusMiles ?? 100));

            var withDistance = restaurants
                .Select(r => new { Restaurant = r, DistanceMiles = HaversineMiles(lat, lon, r.Latitude, r.Longitude) })
                .ToList();

            var zipTrim = zip?.Trim();
            var isZipSearch = !string.IsNullOrEmpty(zipTrim) && zipTrim.Length >= 5;
            var zip5 = isZipSearch ? (zipTrim!.Length >= 5 ? zipTrim[..5] : zipTrim) : "";

            var ordered = withDistance
                .Where(x => x.DistanceMiles <= radiusMi || (isZipSearch && !string.IsNullOrEmpty(x.Restaurant.Address) && x.Restaurant.Address.Contains(zip5)))
                .OrderBy(x => x.DistanceMiles)
                .ThenBy(x => x.Restaurant.Name)
                .Skip(skip)
                .Take(take)
                .ToList();

            return ordered.Select(x => MapToDto(x.Restaurant)).ToList();
        }

        // Default ordering by name, then skip/take
        return restaurants
            .OrderBy(r => r.Name)
            .Skip(skip)
            .Take(take)
            .Select(MapToDto)
            .ToList();
    }

    private static double HaversineMiles(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 3959; // Earth radius in miles
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
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

    public async Task<List<string>> GetSearchSuggestionsAsync(string query, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            return new List<string>();
        }

        var searchTerm = query.ToLower().Trim();

        // Get unique addresses and restaurant names that match the query
        var addresses = await _context.Restaurants
            .Where(r => r.IsActive && r.Address.ToLower().Contains(searchTerm))
            .Select(r => r.Address)
            .Distinct()
            .Take(maxResults)
            .ToListAsync();

        var restaurantNames = await _context.Restaurants
            .Where(r => r.IsActive && r.Name.ToLower().Contains(searchTerm))
            .Select(r => r.Name)
            .Distinct()
            .Take(maxResults)
            .ToListAsync();

        // Combine and deduplicate, prioritizing addresses
        var suggestions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var address in addresses)
        {
            if (suggestions.Count >= maxResults) break;
            suggestions.Add(address);
        }

        foreach (var name in restaurantNames)
        {
            if (suggestions.Count >= maxResults) break;
            suggestions.Add(name);
        }

        return suggestions.Take(maxResults).ToList();
    }

    // Vendor endpoints
    public async Task<List<RestaurantDto>> GetVendorRestaurantsAsync(Guid vendorId)
    {
        var restaurants = await _context.Restaurants
            .Include(r => r.DeliveryZones)
            .Include(r => r.Hours)
            .Where(r => r.OwnerId == vendorId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return restaurants.Select(MapToDto).ToList();
    }

    public async Task<bool> DeleteRestaurantAsync(Guid restaurantId, Guid vendorId)
    {
        var restaurant = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.RestaurantId == restaurantId && r.OwnerId == vendorId);

        if (restaurant == null)
            return false;

        // Soft delete - set IsActive to false
        restaurant.IsActive = false;
        restaurant.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Invalidate cache
        await _redis.DeleteAsync($"restaurant:{restaurantId}");
        await _redis.DeleteAsync($"restaurant:dto:{restaurantId}");

        _logger.LogInformation("Vendor {VendorId} deleted restaurant {RestaurantId}", vendorId, restaurantId);
        return true;
    }

    // Admin endpoints
    public async Task<List<RestaurantDto>> GetAllRestaurantsAsync(int skip = 0, int take = 100)
    {
        var restaurants = await _context.Restaurants
            .Include(r => r.DeliveryZones)
            .Include(r => r.Hours)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return restaurants.Select(MapToDto).ToList();
    }

    public async Task<bool> AdminUpdateRestaurantAsync(Guid restaurantId, UpdateRestaurantDto dto)
    {
        var restaurant = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.RestaurantId == restaurantId);

        if (restaurant == null)
            return false;

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

        _logger.LogInformation("Admin updated restaurant {RestaurantId}", restaurantId);
        return true;
    }

    public async Task<bool> AdminDeleteRestaurantAsync(Guid restaurantId)
    {
        var restaurant = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.RestaurantId == restaurantId);

        if (restaurant == null)
            return false;

        // Hard delete for admin
        _context.Restaurants.Remove(restaurant);
        await _context.SaveChangesAsync();

        // Invalidate cache
        await _redis.DeleteAsync($"restaurant:{restaurantId}");
        await _redis.DeleteAsync($"restaurant:dto:{restaurantId}");

        _logger.LogInformation("Admin deleted restaurant {RestaurantId}", restaurantId);
        return true;
    }

    public async Task<bool> AdminToggleRestaurantStatusAsync(Guid restaurantId, bool isActive)
    {
        var restaurant = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.RestaurantId == restaurantId);

        if (restaurant == null)
            return false;

        restaurant.IsActive = isActive;
        restaurant.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Invalidate cache
        await _redis.DeleteAsync($"restaurant:{restaurantId}");
        await _redis.DeleteAsync($"restaurant:dto:{restaurantId}");

        _logger.LogInformation("Admin toggled restaurant {RestaurantId} status to {IsActive}", restaurantId, isActive);
        return true;
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
            DeliveryZones = (restaurant.DeliveryZones ?? Enumerable.Empty<DeliveryZone>()).Select(z => new DeliveryZoneDto
            {
                ZoneId = z.ZoneId,
                Name = z.Name,
                Coordinates = System.Text.Json.JsonSerializer.Deserialize<List<CoordinateDto>>(z.PolygonCoordinatesJson ?? "[]") ?? new(),
                DeliveryFee = z.DeliveryFee,
                EstimatedMinutes = z.EstimatedMinutes,
                MinimumOrderAmount = z.MinimumOrderAmount
            }).ToList(),
            Hours = (restaurant.Hours ?? Enumerable.Empty<RestaurantHours>()).Select(h => new RestaurantHoursDto
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
    double? Latitude,
    double? Longitude);

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
