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
            _logger.LogError(ex, "Failed to create cart: {Message}, StackTrace: {StackTrace}, InnerException: {InnerException}", 
                ex.Message, ex.StackTrace, ex.InnerException?.Message);
            return StatusCode(500, new { message = $"Failed to create cart: {ex.Message}", details = ex.ToString() });
        }
    }

    [HttpGet("cart")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> GetCartByCustomer()
    {
        try
        {
            _logger.LogInformation("OrderService GetCartByCustomer called");
            _logger.LogInformation("User.Identity.IsAuthenticated: {IsAuthenticated}", User.Identity?.IsAuthenticated ?? false);
            
            // Check if Authorization header is present
            var hasAuthHeader = Request.Headers.TryGetValue("Authorization", out var authHeader);
            _logger.LogInformation("Authorization header present: {HasAuthHeader}", hasAuthHeader);
            
            if (hasAuthHeader)
            {
                var authHeaderValue = authHeader.ToString();
                _logger.LogInformation("Authorization header value: {AuthHeader}", 
                    authHeaderValue.Length > 30 ? authHeaderValue.Substring(0, 30) + "..." : authHeaderValue);
            }
            
            // Allow both authenticated and unauthenticated users
            Guid? customerId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _logger.LogInformation("User is authenticated, userIdClaim: {UserIdClaim}", userIdClaim ?? "null");
                if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                {
                    customerId = userId;
                    _logger.LogInformation("Extracted customerId: {CustomerId}", customerId);
                }
                else
                {
                    _logger.LogWarning("Failed to parse userIdClaim as Guid: {UserIdClaim}", userIdClaim);
                }
            }
            else
            {
                _logger.LogInformation("User is not authenticated");
                if (hasAuthHeader)
                {
                    _logger.LogWarning("Authorization header present but user not authenticated - JWT validation may have failed");
                }
            }

            // If authenticated, try to get cart by customer
            if (customerId.HasValue)
            {
                _logger.LogInformation("Getting cart for customerId: {CustomerId}", customerId.Value);
                var cart = await _orderService.GetCartByCustomerAsync(customerId.Value);
                if (cart != null)
                {
                    _logger.LogInformation("Found cart for customerId {CustomerId}: CartId={CartId}, ItemCount={ItemCount}", 
                        customerId.Value, cart.CartId, cart.Items?.Count ?? 0);
                    return Ok(cart);
                }
                else
                {
                    _logger.LogInformation("No cart found for customerId: {CustomerId}", customerId.Value);
                }
            }

            // If no cart found for customer or not authenticated, return NotFound
            _logger.LogInformation("Returning NotFound - customerId: {CustomerId}, isAuthenticated: {IsAuthenticated}", 
                customerId, User.Identity?.IsAuthenticated ?? false);
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

            // Log cart details before returning
            _logger.LogInformation("GetCart: Returning cart {CartId} - ItemCount={ItemCount}, Subtotal={Subtotal}, Tax={Tax}, DeliveryFee={DeliveryFee}, Total={Total}", 
                cartId, cart.Items?.Count ?? 0, cart.Subtotal, cart.Tax, cart.DeliveryFee, cart.Total);
            
            if (cart.Items != null && cart.Items.Any())
            {
                decimal calculatedSubtotal = 0;
                foreach (var item in cart.Items)
                {
                    var expectedTotalPrice = item.Quantity * item.UnitPrice;
                    calculatedSubtotal += item.TotalPrice;
                    _logger.LogInformation("GetCart: Cart item - CartItemId={CartItemId}, MenuItemId={MenuItemId}, Name={Name}, Quantity={Quantity}, UnitPrice={UnitPrice}, TotalPrice={TotalPrice}, ExpectedTotalPrice={ExpectedTotalPrice}", 
                        item.CartItemId, item.MenuItemId, item.Name, item.Quantity, item.UnitPrice, item.TotalPrice, expectedTotalPrice);
                }
                _logger.LogInformation("GetCart: Calculated subtotal from items: {CalculatedSubtotal}, Cart.Subtotal: {CartSubtotal}", 
                    calculatedSubtotal, cart.Subtotal);
                
                if (calculatedSubtotal != cart.Subtotal)
                {
                    _logger.LogError("GetCart: SUBTOTAL MISMATCH! Calculated={Calculated}, Cart.Subtotal={CartSubtotal}", 
                        calculatedSubtotal, cart.Subtotal);
                }
            }

            return Ok(cart);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cart");
            return StatusCode(500, new { message = "Failed to get cart" });
        }
    }

    [HttpPost("cart/{cartId}/items")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> AddItemToCart(Guid cartId, [FromBody] AddCartItemRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("AddItemToCart: Request body is null");
                return BadRequest(new { message = "Request body is required" });
            }

            _logger.LogInformation("AddItemToCart: CartId={CartId}, MenuItemId={MenuItemId}, Quantity={Quantity}, Price={Price}", 
                cartId, request.MenuItemId, request.Quantity, request.Price);

            await _orderService.AddItemToCartAsync(
                cartId,
                request.MenuItemId,
                request.Name,
                request.Price,
                request.Quantity,
                request.Options);

            _logger.LogInformation("AddItemToCart: Successfully added item to cart {CartId}", cartId);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "AddItemToCart: Invalid operation - {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddItemToCart: Failed to add item to cart - {Message}, StackTrace: {StackTrace}", 
                ex.Message, ex.StackTrace);
            return StatusCode(500, new { message = $"Failed to add item to cart: {ex.Message}" });
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
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> RemoveCartItem(Guid cartId, Guid cartItemId)
    {
        try
        {
            _logger.LogInformation("RemoveCartItem: CartId={CartId}, CartItemId={CartItemId}", cartId, cartItemId);
            
            await _orderService.RemoveCartItemAsync(cartId, cartItemId);
            
            _logger.LogInformation("RemoveCartItem: Successfully removed item {CartItemId} from cart {CartId}", cartItemId, cartId);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "RemoveCartItem: Invalid operation - {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemoveCartItem: Failed to remove cart item - {Message}, StackTrace: {StackTrace}", 
                ex.Message, ex.StackTrace);
            return StatusCode(500, new { message = $"Failed to remove cart item: {ex.Message}" });
        }
    }

    [HttpDelete("cart/{cartId}")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> ClearCart(Guid cartId)
    {
        try
        {
            _logger.LogInformation("ClearCart: CartId={CartId}", cartId);
            
            await _orderService.ClearCartAsync(cartId);
            
            _logger.LogInformation("ClearCart: Successfully cleared cart {CartId}", cartId);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            // If cart doesn't exist, that's fine - it's already cleared
            if (ex.Message.Contains("not found"))
            {
                _logger.LogInformation("ClearCart: Cart {CartId} not found - already cleared", cartId);
                return Ok(new { message = "Cart already cleared" });
            }
            _logger.LogWarning(ex, "ClearCart: Invalid operation - {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClearCart: Failed to clear cart - {Message}", ex.Message);
            return StatusCode(500, new { message = $"Failed to clear cart: {ex.Message}" });
        }
    }

    [HttpPost("cart/merge")]
    [AllowAnonymous]
    public async Task<IActionResult> MergeCarts([FromQuery] Guid guestCartId, [FromQuery] Guid userCartId)
    {
        try
        {
            // Extract customerId from JWT if authenticated
            Guid? customerId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                {
                    customerId = userId;
                }
            }

            if (userCartId == Guid.Empty)
            {
                // User has no cart, just transfer guest cart by updating customerId
                var guestCart = await _orderService.GetCartAsync(guestCartId);
                if (guestCart == null)
                {
                    return NotFound(new { message = "Guest cart not found" });
                }

                if (customerId.HasValue)
                {
                    // Update cart's customerId in database
                    guestCart.CustomerId = customerId;
                    // Save changes - we'll need to update the cart in the database
                    // For now, just return the guest cart ID - the merge logic will handle it
                    return Ok(new { cartId = guestCartId });
                }

                return BadRequest(new { message = "User ID required for cart transfer" });
            }

            var mergedCartId = await _orderService.MergeCartsAsync(guestCartId, userCartId);
            return Ok(new { cartId = mergedCartId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge carts");
            return StatusCode(500, new { message = "Failed to merge carts" });
        }
    }

    [HttpPost("place")]
    [Authorize] // Place order requires authentication
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("PlaceOrder: Request body is null");
                return BadRequest(new { message = "Request body is required" });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogError("PlaceOrder: User ID claim is missing");
                return Unauthorized(new { message = "User ID claim is missing" });
            }

            if (!Guid.TryParse(userIdClaim, out var customerId))
            {
                _logger.LogError("PlaceOrder: Invalid user ID format: {UserIdClaim}", userIdClaim);
                return BadRequest(new { message = "Invalid user ID format" });
            }

            _logger.LogInformation("PlaceOrder: CartId={CartId}, CustomerId={CustomerId}, DeliveryAddress={DeliveryAddress}", 
                request.CartId, customerId, request.DeliveryAddress);

            var idempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString();

            var orderId = await _orderService.PlaceOrderAsync(
                request.CartId,
                customerId,
                request.DeliveryAddress,
                idempotencyKey);

            _logger.LogInformation("PlaceOrder: Successfully placed order {OrderId} for customer {CustomerId}", orderId, customerId);
            return Ok(new { orderId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "PlaceOrder: Invalid operation - {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PlaceOrder: Failed to place order - {Message}", ex.Message);
            return StatusCode(500, new { message = $"Failed to place order: {ex.Message}" });
        }
    }

    [HttpGet]
    [Authorize] // Get orders requires authentication
    public async Task<IActionResult> GetOrders()
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _logger.LogInformation("GetOrders: User.Identity.IsAuthenticated={IsAuthenticated}, UserIdClaim={UserIdClaim}", 
                User.Identity?.IsAuthenticated ?? false, userIdClaim);
            
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogError("GetOrders: User ID claim is missing");
                return Unauthorized(new { message = "User ID claim is missing" });
            }
            
            if (!Guid.TryParse(userIdClaim, out var customerId))
            {
                _logger.LogError("GetOrders: Invalid user ID format: {UserIdClaim}", userIdClaim);
                return BadRequest(new { message = "Invalid user ID format" });
            }
            
            _logger.LogInformation("GetOrders: CustomerId={CustomerId}", customerId);
            
            var orders = await _orderService.GetOrdersByCustomerAsync(customerId);
            
            _logger.LogInformation("GetOrders: Returning {OrderCount} orders for CustomerId={CustomerId}", orders.Count, customerId);
            
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOrders: Failed to get orders - {Message}, StackTrace: {StackTrace}", 
                ex.Message, ex.StackTrace);
            return StatusCode(500, new { message = $"Failed to get orders: {ex.Message}" });
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

    [HttpGet("vendor/restaurants/{restaurantId}/orders")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetVendorOrders(Guid restaurantId)
    {
        try
        {
            var orders = await _orderService.GetOrdersByRestaurantAsync(restaurantId);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vendor orders");
            return StatusCode(500, new { message = "Failed to get vendor orders" });
        }
    }

    [HttpPut("{orderId}/status")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            _logger.LogInformation("UpdateOrderStatus: OrderId={OrderId}, Request={@Request}", orderId, request);
            
            if (request == null)
            {
                _logger.LogWarning("UpdateOrderStatus: Request body is null");
                return BadRequest(new { message = "Request body is required" });
            }
            
            if (string.IsNullOrWhiteSpace(request.Status))
            {
                _logger.LogWarning("UpdateOrderStatus: Status is null or empty");
                return BadRequest(new { message = "Status is required" });
            }

            var order = await _orderService.GetOrderAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("UpdateOrderStatus: Order not found - OrderId={OrderId}", orderId);
                return NotFound(new { message = "Order not found" });
            }

            _logger.LogInformation("UpdateOrderStatus: Updating order - OrderId={OrderId}, OldStatus={OldStatus}, NewStatus={NewStatus}, Notes={Notes}",
                orderId, order.Status, request.Status, request.Notes ?? "null");

            var success = await _orderService.UpdateOrderStatusAsync(orderId, request.Status, request.Notes);
            if (!success)
            {
                _logger.LogWarning("UpdateOrderStatus: UpdateOrderStatusAsync returned false - OrderId={OrderId}", orderId);
                return BadRequest(new { message = "Failed to update order status" });
            }

            _logger.LogInformation("UpdateOrderStatus: Successfully updated order status - OrderId={OrderId}, NewStatus={NewStatus}", 
                orderId, request.Status);
            return Ok(new { message = "Order status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateOrderStatus: Exception occurred - OrderId={OrderId}, Exception={Exception}", 
                orderId, ex.ToString());
            return StatusCode(500, new { message = "Failed to update order status", error = ex.Message });
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
public record UpdateOrderStatusRequest(
    string Status,
    string? Notes
);