using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionalEats.RestaurantService.Services;

namespace TraditionalEats.RestaurantService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RestaurantController : ControllerBase
{
    private readonly IRestaurantService _restaurantService;
    private readonly ILogger<RestaurantController> _logger;

    public RestaurantController(IRestaurantService restaurantService, ILogger<RestaurantController> logger)
    {
        _restaurantService = restaurantService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> CreateRestaurant([FromBody] CreateRestaurantDto dto)
    {
        try
        {
            var ownerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var restaurantId = await _restaurantService.CreateRestaurantAsync(ownerId, dto);
            return Ok(new { restaurantId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create restaurant");
            return StatusCode(500, new { message = "Failed to create restaurant" });
        }
    }

    [HttpGet("{restaurantId}")]
    public async Task<IActionResult> GetRestaurant(Guid restaurantId)
    {
        try
        {
            var restaurant = await _restaurantService.GetRestaurantAsync(restaurantId);
            if (restaurant == null)
            {
                return NotFound(new { message = "Restaurant not found" });
            }
            return Ok(restaurant);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get restaurant");
            return StatusCode(500, new { message = "Failed to get restaurant" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetRestaurants(
        [FromQuery] string? location,
        [FromQuery] string? cuisineType,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        try
        {
            var restaurants = await _restaurantService.GetRestaurantsAsync(location, cuisineType, latitude, longitude, skip, take);
            return Ok(restaurants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get restaurants");
            return StatusCode(500, new { message = "Failed to get restaurants" });
        }
    }

    [HttpPut("{restaurantId}")]
    [Authorize]
    public async Task<IActionResult> UpdateRestaurant(Guid restaurantId, [FromBody] UpdateRestaurantDto dto)
    {
        try
        {
            var ownerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var success = await _restaurantService.UpdateRestaurantAsync(restaurantId, ownerId, dto);
            if (!success)
            {
                return NotFound(new { message = "Restaurant not found or you don't have permission" });
            }
            return Ok(new { message = "Restaurant updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update restaurant");
            return StatusCode(500, new { message = "Failed to update restaurant" });
        }
    }

    [HttpPost("{restaurantId}/delivery-zones")]
    [Authorize]
    public async Task<IActionResult> AddDeliveryZone(Guid restaurantId, [FromBody] CreateDeliveryZoneDto dto)
    {
        try
        {
            var ownerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var zoneId = await _restaurantService.AddDeliveryZoneAsync(restaurantId, ownerId, dto);
            return Ok(new { zoneId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add delivery zone");
            return StatusCode(500, new { message = "Failed to add delivery zone" });
        }
    }

    [HttpGet("{restaurantId}/delivery-zones")]
    public async Task<IActionResult> GetDeliveryZones(Guid restaurantId)
    {
        try
        {
            var zones = await _restaurantService.GetDeliveryZonesAsync(restaurantId);
            return Ok(zones);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get delivery zones");
            return StatusCode(500, new { message = "Failed to get delivery zones" });
        }
    }

    [HttpPut("{restaurantId}/hours")]
    [Authorize]
    public async Task<IActionResult> SetRestaurantHours(Guid restaurantId, [FromBody] List<RestaurantHoursDto> hours)
    {
        try
        {
            var ownerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var success = await _restaurantService.SetRestaurantHoursAsync(restaurantId, ownerId, hours);
            if (!success)
            {
                return NotFound(new { message = "Restaurant not found or you don't have permission" });
            }
            return Ok(new { message = "Restaurant hours updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set restaurant hours");
            return StatusCode(500, new { message = "Failed to set restaurant hours" });
        }
    }

    [HttpGet("{restaurantId}/hours")]
    public async Task<IActionResult> GetRestaurantHours(Guid restaurantId)
    {
        try
        {
            var hours = await _restaurantService.GetRestaurantHoursAsync(restaurantId);
            return Ok(hours);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get restaurant hours");
            return StatusCode(500, new { message = "Failed to get restaurant hours" });
        }
    }

    [HttpGet("{restaurantId}/is-open")]
    public async Task<IActionResult> IsRestaurantOpen(Guid restaurantId)
    {
        try
        {
            var isOpen = await _restaurantService.IsRestaurantOpenAsync(restaurantId);
            return Ok(new { isOpen });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if restaurant is open");
            return StatusCode(500, new { message = "Failed to check if restaurant is open" });
        }
    }

    [HttpGet("search-suggestions")]
    public async Task<IActionResult> GetSearchSuggestions([FromQuery] string query, [FromQuery] int maxResults = 10)
    {
        try
        {
            var suggestions = await _restaurantService.GetSearchSuggestionsAsync(query, maxResults);
            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get search suggestions");
            return StatusCode(500, new { message = "Failed to get search suggestions" });
        }
    }

    // Vendor endpoints
    [HttpGet("vendor/my-restaurants")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetMyRestaurants()
    {
        try
        {
            var vendorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var restaurants = await _restaurantService.GetVendorRestaurantsAsync(vendorId);
            return Ok(restaurants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vendor restaurants");
            return StatusCode(500, new { message = "Failed to get vendor restaurants" });
        }
    }

    [HttpDelete("vendor/{restaurantId}")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> DeleteMyRestaurant(Guid restaurantId)
    {
        try
        {
            var vendorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var success = await _restaurantService.DeleteRestaurantAsync(restaurantId, vendorId);
            if (!success)
            {
                return NotFound(new { message = "Restaurant not found or you don't have permission" });
            }
            return Ok(new { message = "Restaurant deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete restaurant");
            return StatusCode(500, new { message = "Failed to delete restaurant" });
        }
    }

    // Admin endpoints
    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllRestaurants([FromQuery] int skip = 0, [FromQuery] int take = 100)
    {
        try
        {
            var restaurants = await _restaurantService.GetAllRestaurantsAsync(skip, take);
            return Ok(restaurants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all restaurants");
            return StatusCode(500, new { message = "Failed to get all restaurants" });
        }
    }

    [HttpPut("admin/{restaurantId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUpdateRestaurant(Guid restaurantId, [FromBody] UpdateRestaurantDto dto)
    {
        try
        {
            var success = await _restaurantService.AdminUpdateRestaurantAsync(restaurantId, dto);
            if (!success)
            {
                return NotFound(new { message = "Restaurant not found" });
            }
            return Ok(new { message = "Restaurant updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update restaurant");
            return StatusCode(500, new { message = "Failed to update restaurant" });
        }
    }

    [HttpDelete("admin/{restaurantId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminDeleteRestaurant(Guid restaurantId)
    {
        try
        {
            var success = await _restaurantService.AdminDeleteRestaurantAsync(restaurantId);
            if (!success)
            {
                return NotFound(new { message = "Restaurant not found" });
            }
            return Ok(new { message = "Restaurant deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete restaurant");
            return StatusCode(500, new { message = "Failed to delete restaurant" });
        }
    }

    [HttpPatch("admin/{restaurantId}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminToggleRestaurantStatus(Guid restaurantId, [FromBody] ToggleStatusRequest request)
    {
        try
        {
            var success = await _restaurantService.AdminToggleRestaurantStatusAsync(restaurantId, request.IsActive);
            if (!success)
            {
                return NotFound(new { message = "Restaurant not found" });
            }
            return Ok(new { message = $"Restaurant status set to {(request.IsActive ? "Active" : "Inactive")}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle restaurant status");
            return StatusCode(500, new { message = "Failed to toggle restaurant status" });
        }
    }
}

public record ToggleStatusRequest(bool IsActive);
