using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionalEats.OrderService.Services;

namespace TraditionalEats.OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrderController> _logger;

    public OrderController(IOrderService orderService, ILogger<OrderController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    [HttpPost("cart")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateCart([FromBody] CreateCartRequest? request = null)
    {
        try
        {
            // Log authentication state
            _logger.LogInformation("OrderService CreateCart - User.Identity.IsAuthenticated: {IsAuthenticated}", 
                User.Identity?.IsAuthenticated ?? false);
            
            // Allow both authenticated and unauthenticated users
            Guid? customerId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _logger.LogInformation("OrderService CreateCart - Found userIdClaim: {UserIdClaim}", userIdClaim ?? "null");
                if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                {
                    customerId = userId;
                    _logger.LogInformation("OrderService CreateCart - Extracted customerId: {CustomerId}", customerId);
                }
                else
                {
                    _logger.LogWarning("OrderService CreateCart - Failed to parse userIdClaim as Guid: {UserIdClaim}", userIdClaim);
                }
            }
            else
            {
                _logger.LogInformation("OrderService CreateCart - User is not authenticated");
                // Log Authorization header presence
                if (Request.Headers.ContainsKey("Authorization"))
                {
                    _logger.LogWarning("OrderService CreateCart - Authorization header present but user not authenticated - JWT validation may have failed");
                }
                else
                {
                    _logger.LogInformation("OrderService CreateCart - No Authorization header in request");
                }
            }

            var restaurantId = request?.RestaurantId;
            _logger.LogInformation("OrderService CreateCart - Creating cart with customerId: {CustomerId}, restaurantId: {RestaurantId}", 
                customerId, restaurantId);
            var cartId = await _orderService.CreateCartAsync(customerId, restaurantId);
            _logger.LogInformation("OrderService CreateCart - Created cart with ID: {CartId}", cartId);
            return Ok(new { cartId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create cart");
            return StatusCode(500, new { message = "Failed to create cart" });
        }
    }

    [HttpGet("cart")]
    public async Task<IActionResult> GetCartByCustomer()
    {
        try
        {
            // Allow both authenticated and unauthenticated users
            Guid? customerId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                {
                    customerId = userId;
                }
            }

            // If authenticated, try to get cart by customer
            if (customerId.HasValue)
            {
                var cart = await _orderService.GetCartByCustomerAsync(customerId.Value);
                if (cart != null)
                    return Ok(cart);
            }

            // If no cart found for customer or not authenticated, return NotFound
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cart");
            return StatusCode(500, new { message = "Failed to get cart" });
        }
    }

    [HttpGet("cart/{cartId}")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> GetCart(Guid cartId)
    {
        try
        {
            var cart = await _orderService.GetCartAsync(cartId);
            if (cart == null)
                return NotFound();

            return Ok(cart);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cart");
            return StatusCode(500, new { message = "Failed to get cart" });
        }
    }

    [HttpPost("cart/{cartId}/items")]
    public async Task<IActionResult> AddItemToCart(Guid cartId, [FromBody] AddCartItemRequest request)
    {
        try
        {
            await _orderService.AddItemToCartAsync(
                cartId,
                request.MenuItemId,
                request.Name,
                request.Price,
                request.Quantity,
                request.Options);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add item to cart");
            return StatusCode(500, new { message = "Failed to add item to cart" });
        }
    }

    [HttpPut("cart/{cartId}/items/{cartItemId}")]
    public async Task<IActionResult> UpdateCartItemQuantity(Guid cartId, Guid cartItemId, [FromBody] UpdateCartItemRequest request)
    {
        try
        {
            await _orderService.UpdateCartItemQuantityAsync(cartId, cartItemId, request.Quantity);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update cart item");
            return StatusCode(500, new { message = "Failed to update cart item" });
        }
    }

    [HttpDelete("cart/{cartId}/items/{cartItemId}")]
    public async Task<IActionResult> RemoveCartItem(Guid cartId, Guid cartItemId)
    {
        try
        {
            await _orderService.RemoveCartItemAsync(cartId, cartItemId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove cart item");
            return StatusCode(500, new { message = "Failed to remove cart item" });
        }
    }

    [HttpDelete("cart/{cartId}")]
    public async Task<IActionResult> ClearCart(Guid cartId)
    {
        try
        {
            await _orderService.ClearCartAsync(cartId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cart");
            return StatusCode(500, new { message = "Failed to clear cart" });
        }
    }

    [HttpPost("place")]
    [Authorize] // Place order requires authentication
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        try
        {
            var customerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var idempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString();

            var orderId = await _orderService.PlaceOrderAsync(
                request.CartId,
                customerId,
                request.DeliveryAddress,
                idempotencyKey);

            return Ok(new { orderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to place order");
            return StatusCode(500, new { message = "Failed to place order" });
        }
    }

    [HttpGet]
    [Authorize] // Get orders requires authentication
    public async Task<IActionResult> GetOrders()
    {
        try
        {
            var customerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var orders = await _orderService.GetOrdersByCustomerAsync(customerId);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get orders");
            return StatusCode(500, new { message = "Failed to get orders" });
        }
    }

    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrder(Guid orderId)
    {
        try
        {
            var order = await _orderService.GetOrderAsync(orderId);
            if (order == null)
                return NotFound();

            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order");
            return StatusCode(500, new { message = "Failed to get order" });
        }
    }
}

public record CreateCartRequest(Guid? RestaurantId);
public record AddCartItemRequest(
    Guid MenuItemId,
    string Name,
    decimal Price,
    int Quantity,
    Dictionary<string, string>? Options
);
public record UpdateCartItemRequest(int Quantity);
public record PlaceOrderRequest(
    Guid CartId,
    string DeliveryAddress,
    string? IdempotencyKey
);
