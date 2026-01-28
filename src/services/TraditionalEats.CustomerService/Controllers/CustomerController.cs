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
            var customer = await _customerService.GetCustomerByUserIdAsync(userId);

            if (customer == null)
            {
                return NotFound(new { message = "Customer not found" });
            }

            return Ok(customer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get customer");
            return StatusCode(500, new { message = "Failed to get customer" });
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
