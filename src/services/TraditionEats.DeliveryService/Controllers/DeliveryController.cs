using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionEats.DeliveryService.Services;

namespace TraditionEats.DeliveryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeliveryController : ControllerBase
{
    private readonly IDeliveryService _deliveryService;
    private readonly ILogger<DeliveryController> _logger;

    public DeliveryController(IDeliveryService deliveryService, ILogger<DeliveryController> logger)
    {
        _deliveryService = deliveryService;
        _logger = logger;
    }

    [HttpPost("drivers/register")]
    [Authorize]
    public async Task<IActionResult> RegisterDriver([FromBody] RegisterDriverDto dto)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var driverId = await _deliveryService.RegisterDriverAsync(userId, dto);
            return Ok(new { driverId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register driver");
            return StatusCode(500, new { message = "Failed to register driver" });
        }
    }

    [HttpGet("drivers/{driverId}")]
    [Authorize]
    public async Task<IActionResult> GetDriver(Guid driverId)
    {
        try
        {
            var driver = await _deliveryService.GetDriverAsync(driverId);
            if (driver == null)
            {
                return NotFound(new { message = "Driver not found" });
            }
            return Ok(driver);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get driver");
            return StatusCode(500, new { message = "Failed to get driver" });
        }
    }

    [HttpGet("drivers/me")]
    [Authorize]
    public async Task<IActionResult> GetMyDriver()
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var driver = await _deliveryService.GetDriverByUserIdAsync(userId);
            if (driver == null)
            {
                return NotFound(new { message = "Driver not found" });
            }
            return Ok(driver);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get driver");
            return StatusCode(500, new { message = "Failed to get driver" });
        }
    }

    [HttpPatch("drivers/{driverId}/availability")]
    [Authorize]
    public async Task<IActionResult> UpdateAvailability(Guid driverId, [FromBody] UpdateAvailabilityRequest request)
    {
        try
        {
            var success = await _deliveryService.UpdateDriverAvailabilityAsync(driverId, request.IsAvailable);
            if (!success)
            {
                return NotFound(new { message = "Driver not found" });
            }
            return Ok(new { message = "Availability updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update driver availability");
            return StatusCode(500, new { message = "Failed to update driver availability" });
        }
    }

    [HttpPost("drivers/{driverId}/location")]
    [Authorize]
    public async Task<IActionResult> UpdateLocation(Guid driverId, [FromBody] UpdateLocationRequest request)
    {
        try
        {
            var success = await _deliveryService.UpdateDriverLocationAsync(driverId, request.Latitude, request.Longitude);
            if (!success)
            {
                return NotFound(new { message = "Driver not found" });
            }
            return Ok(new { message = "Location updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update driver location");
            return StatusCode(500, new { message = "Failed to update driver location" });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,OrderService")]
    public async Task<IActionResult> CreateDelivery([FromBody] CreateDeliveryRequest request)
    {
        try
        {
            var deliveryId = await _deliveryService.CreateDeliveryAsync(request.OrderId, request.Delivery);
            return Ok(new { deliveryId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create delivery");
            return StatusCode(500, new { message = "Failed to create delivery" });
        }
    }

    [HttpGet("{deliveryId}")]
    [Authorize]
    public async Task<IActionResult> GetDelivery(Guid deliveryId)
    {
        try
        {
            var delivery = await _deliveryService.GetDeliveryAsync(deliveryId);
            if (delivery == null)
            {
                return NotFound(new { message = "Delivery not found" });
            }
            return Ok(delivery);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get delivery");
            return StatusCode(500, new { message = "Failed to get delivery" });
        }
    }

    [HttpGet("order/{orderId}")]
    [Authorize]
    public async Task<IActionResult> GetDeliveryByOrderId(Guid orderId)
    {
        try
        {
            var delivery = await _deliveryService.GetDeliveryByOrderIdAsync(orderId);
            if (delivery == null)
            {
                return NotFound(new { message = "Delivery not found" });
            }
            return Ok(delivery);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get delivery");
            return StatusCode(500, new { message = "Failed to get delivery" });
        }
    }

    [HttpPost("{deliveryId}/assign")]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> AssignDriver(Guid deliveryId, [FromBody] AssignDriverRequest request)
    {
        try
        {
            var success = await _deliveryService.AssignDriverAsync(deliveryId, request.DriverId);
            if (!success)
            {
                return BadRequest(new { message = "Failed to assign driver. Delivery or driver not found, or driver not available." });
            }
            return Ok(new { message = "Driver assigned successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign driver");
            return StatusCode(500, new { message = "Failed to assign driver" });
        }
    }

    [HttpPatch("{deliveryId}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateDeliveryStatus(Guid deliveryId, [FromBody] UpdateDeliveryStatusRequest request)
    {
        try
        {
            var success = await _deliveryService.UpdateDeliveryStatusAsync(
                deliveryId, 
                request.Status, 
                request.Latitude, 
                request.Longitude);
            
            if (!success)
            {
                return NotFound(new { message = "Delivery not found" });
            }
            return Ok(new { message = "Delivery status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update delivery status");
            return StatusCode(500, new { message = "Failed to update delivery status" });
        }
    }

    [HttpGet("drivers/available")]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> GetAvailableDrivers(
        [FromQuery] double? latitude,
        [FromQuery] double? longitude)
    {
        try
        {
            var drivers = await _deliveryService.GetAvailableDriversAsync(latitude, longitude);
            return Ok(drivers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available drivers");
            return StatusCode(500, new { message = "Failed to get available drivers" });
        }
    }

    [HttpGet("{deliveryId}/tracking")]
    [Authorize]
    public async Task<IActionResult> GetDeliveryTracking(Guid deliveryId)
    {
        try
        {
            var tracking = await _deliveryService.GetDeliveryTrackingAsync(deliveryId);
            return Ok(tracking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get delivery tracking");
            return StatusCode(500, new { message = "Failed to get delivery tracking" });
        }
    }
}

public record UpdateAvailabilityRequest(bool IsAvailable);
public record UpdateLocationRequest(double Latitude, double Longitude);
public record CreateDeliveryRequest(Guid OrderId, CreateDeliveryDto Delivery);
public record AssignDriverRequest(Guid DriverId);
public record UpdateDeliveryStatusRequest(string Status, double? Latitude = null, double? Longitude = null);
