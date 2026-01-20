using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionEats.OrderService.Services;

namespace TraditionEats.OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
    public async Task<IActionResult> CreateCart()
    {
        try
        {
            var customerId = User.FindFirstValue(ClaimTypes.NameIdentifier) != null
                ? Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : (Guid?)null;

            var cartId = await _orderService.CreateCartAsync(customerId);
            return Ok(new { cartId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create cart");
            return StatusCode(500, new { message = "Failed to create cart" });
        }
    }

    [HttpGet("cart/{cartId}")]
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

    [HttpPost("place")]
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

public record AddCartItemRequest(
    Guid MenuItemId,
    string Name,
    decimal Price,
    int Quantity,
    Dictionary<string, string>? Options
);

public record PlaceOrderRequest(
    Guid CartId,
    string DeliveryAddress,
    string? IdempotencyKey
);
