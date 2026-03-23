using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionalEats.CustomerService.Services;

namespace TraditionalEats.CustomerService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomerController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ILogger<CustomerController> _logger;

    public CustomerController(ICustomerService customerService, ILogger<CustomerController> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var customerId = await _customerService.CreateCustomerAsync(
                userId,
                request.FirstName,
                request.LastName,
                request.Email,
                request.PhoneNumber ?? string.Empty,
                null);

            return Ok(new { customerId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create customer");
            return StatusCode(500, new { message = "Failed to create customer" });
        }
    }

    /// <summary>
    /// Internal endpoint used by IdentityService right after registration to create the customer profile.
    /// NOTE: Currently AllowAnonymous (dev). In production, protect with service-to-service auth (API key/JWT/mTLS).
    /// </summary>
    [HttpPost("internal")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateCustomerInternal([FromBody] CreateCustomerInternalRequest request)
    {
        try
        {
            // Prevent duplicates if registration retries
            var existing = await _customerService.GetCustomerByUserIdAsync(request.UserId);
            if (existing != null)
            {
                return Ok(new { customerId = existing.CustomerId, alreadyExists = true });
            }

            var customerId = await _customerService.CreateCustomerAsync(
                request.UserId,
                request.FirstName,
                request.LastName,
                request.Email,
                request.PhoneNumber,
                request.DisplayName);

            return Ok(new { customerId, alreadyExists = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create customer (internal)");
            return StatusCode(500, new { message = "Failed to create customer" });
        }
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyCustomer()
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var info = await _customerService.GetCustomerInfoByUserIdAsync(userId);

            if (info == null)
            {
                return NotFound(new { message = "Customer not found" });
            }

            var addresses = await _customerService.GetAddressesAsync(info.CustomerId);
            return Ok(new CustomerProfileDto(
                info.CustomerId,
                info.UserId,
                info.FirstName,
                info.LastName,
                info.Email,
                info.PhoneNumber,
                addresses));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get customer");
            return StatusCode(500, new { message = "Failed to get customer" });
        }
    }

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var updated = await _customerService.UpdateCustomerPIIAsync(
                userId,
                request.FirstName,
                request.LastName,
                request.PhoneNumber);

            if (!updated)
            {
                return NotFound(new { message = "Customer not found" });
            }

            return Ok(new { message = "Profile updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update profile");
            return StatusCode(500, new { message = "Failed to update profile" });
        }
    }

    /// <summary>
    /// Get customer info by user id (identity). Used by NotificationService for order-ready emails/SMS.
    /// AllowAnonymous for server-to-server calls.
    /// </summary>
    [HttpGet("by-user/{userId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCustomerByUserId(Guid userId)
    {
        try
        {
            var customer = await _customerService.GetCustomerInfoByUserIdAsync(userId);
            if (customer == null)
                return NotFound(new { message = "Customer not found" });
            return Ok(customer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get customer by user id");
            return StatusCode(500, new { message = "Failed to get customer" });
        }
    }

    [HttpDelete("by-user/{userId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> DeleteCustomerByUserId(Guid userId)
    {
        try
        {
            var deleted = await _customerService.DeleteCustomerByUserIdAsync(userId);
            if (!deleted)
                return NotFound(new { message = "Customer not found" });
            return Ok(new { message = "Customer deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete customer for userId {UserId}", userId);
            return StatusCode(500, new { message = "Failed to delete customer" });
        }
    }

    [HttpPut("addresses/{addressId:guid}")]
    public async Task<IActionResult> UpdateAddress(Guid addressId, [FromBody] AddressDto request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var updated = await _customerService.UpdateAddressAsync(userId, addressId, request);

            if (!updated)
            {
                return NotFound(new { message = "Address not found" });
            }

            return Ok(new { message = "Address updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update address");
            return StatusCode(500, new { message = "Failed to update address" });
        }
    }

    [HttpDelete("addresses/{addressId:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid addressId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var deleted = await _customerService.DeleteAddressAsync(userId, addressId);

            if (!deleted)
            {
                return NotFound(new { message = "Address not found" });
            }

            return Ok(new { message = "Address deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete address");
            return StatusCode(500, new { message = "Failed to delete address" });
        }
    }

    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress([FromBody] AddressDto request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var customer = await _customerService.GetCustomerByUserIdAsync(userId);

            if (customer == null)
            {
                return NotFound(new { message = "Customer not found" });
            }

            var addressId = await _customerService.AddAddressAsync(customer.CustomerId, request);
            return Ok(new { addressId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add address");
            return StatusCode(500, new { message = "Failed to add address" });
        }
    }

    [HttpGet("addresses")]
    public async Task<IActionResult> GetAddresses()
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var customer = await _customerService.GetCustomerByUserIdAsync(userId);

            if (customer == null)
            {
                return NotFound(new { message = "Customer not found" });
            }

            var addresses = await _customerService.GetAddressesAsync(customer.CustomerId);
            return Ok(addresses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get addresses");
            return StatusCode(500, new { message = "Failed to get addresses" });
        }
    }
}

public record CreateCustomerRequest(string FirstName, string LastName, string Email, string? PhoneNumber);
public record CreateCustomerInternalRequest(Guid UserId, string FirstName, string LastName, string Email, string PhoneNumber, string? DisplayName);
public record CustomerProfileDto(Guid CustomerId, Guid UserId, string FirstName, string LastName, string? Email, string? PhoneNumber, List<AddressDto> Addresses);
/// <summary>Email is accepted but ignored - it cannot be changed (tied to Identity account).</summary>
public record UpdateProfileRequest(string FirstName, string LastName, string? PhoneNumber, string? Email = null);
