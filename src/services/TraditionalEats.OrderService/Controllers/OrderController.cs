using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionalEats.OrderService.Services;
using TraditionalEats.OrderService.Entities;

namespace TraditionalEats.OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrderController> _logger;
    private readonly IConfiguration _configuration;

    public OrderController(IOrderService orderService, ILogger<OrderController> logger, IConfiguration configuration)
    {
        _orderService = orderService;
        _logger = logger;
        _configuration = configuration;
    }

    public record InternalUpdateOrderPaymentRequest(
        string PaymentStatus,
        string? StripePaymentIntentId,
        string? FailureReason);

    /// <summary>
    /// Internal: update order payment state. Called by PaymentService Stripe webhooks.
    /// </summary>
    [HttpPatch("internal/{orderId}/payment")]
    [AllowAnonymous]
    public async Task<IActionResult> InternalUpdateOrderPayment(
        Guid orderId,
        [FromBody] InternalUpdateOrderPaymentRequest request,
        [FromHeader(Name = "X-Internal-Api-Key")] string? apiKey = null)
    {
        var expectedKey = _configuration["InternalApiKey"] ?? _configuration["Services:OrderService:InternalApiKey"];
        if (!string.IsNullOrEmpty(expectedKey) && apiKey != expectedKey)
        {
            _logger.LogWarning("Internal order payment update rejected: missing or invalid API key");
            return Unauthorized(new { message = "Invalid or missing internal API key" });
        }

        if (request == null)
            return BadRequest(new { message = "Request body is required" });

        try
        {
            var ok = await _orderService.UpdateOrderPaymentAsync(
                orderId,
                request.PaymentStatus,
                request.StripePaymentIntentId,
                request.FailureReason);

            if (!ok) return NotFound(new { message = "Order not found" });

            return Ok(new { message = "Payment status updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InternalUpdateOrderPayment failed for {OrderId}", orderId);
            return StatusCode(500, new { message = "Failed to update order payment" });
        }
    }

    // ----------------------------
    // Cart endpoints (UNCHANGED)
    // ----------------------------

    [HttpPost("cart")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateCart([FromBody] CreateCartRequest? request = null)
    {
        try
        {
            _logger.LogInformation("OrderService CreateCart - User.Identity.IsAuthenticated: {IsAuthenticated}",
                User.Identity?.IsAuthenticated ?? false);

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
    [AllowAnonymous]
    public async Task<IActionResult> GetCartByCustomer()
    {
        try
        {
            _logger.LogInformation("OrderService GetCartByCustomer called");
            _logger.LogInformation("User.Identity.IsAuthenticated: {IsAuthenticated}", User.Identity?.IsAuthenticated ?? false);

            var hasAuthHeader = Request.Headers.TryGetValue("Authorization", out var authHeader);
            _logger.LogInformation("Authorization header present: {HasAuthHeader}", hasAuthHeader);

            if (hasAuthHeader)
            {
                var authHeaderValue = authHeader.ToString();
                _logger.LogInformation("Authorization header value: {AuthHeader}",
                    authHeaderValue.Length > 30 ? authHeaderValue.Substring(0, 30) + "..." : authHeaderValue);
            }

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

                _logger.LogInformation("No cart found for customerId: {CustomerId}", customerId.Value);
            }

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
    [AllowAnonymous]
    public async Task<IActionResult> GetCart(Guid cartId)
    {
        try
        {
            var cart = await _orderService.GetCartAsync(cartId);
            if (cart == null)
                return NotFound();

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
    [AllowAnonymous]
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
    [AllowAnonymous]
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
    [AllowAnonymous]
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
                var guestCart = await _orderService.GetCartAsync(guestCartId);
                if (guestCart == null)
                {
                    return NotFound(new { message = "Guest cart not found" });
                }

                if (customerId.HasValue)
                {
                    guestCart.CustomerId = customerId;
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

    // ----------------------------
    // Orders (customer)
    // ----------------------------

    [HttpPost("place")]
    [Authorize]
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

            _logger.LogInformation("PlaceOrder: CartId={CartId}, CustomerId={CustomerId}, DeliveryAddress={DeliveryAddress}, SpecialInstructions={SpecialInstructions}",
                request.CartId, customerId, request.DeliveryAddress, request.SpecialInstructions ?? "none");

            var idempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString();

            var orderId = await _orderService.PlaceOrderAsync(
                request.CartId,
                customerId,
                request.DeliveryAddress,
                request.SpecialInstructions,
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
    [Authorize]
    public async Task<IActionResult> GetOrders()
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            _logger.LogInformation("GetOrders: User.Identity.IsAuthenticated={IsAuthenticated}, UserIdClaim={UserIdClaim}",
                User.Identity?.IsAuthenticated ?? false, userIdClaim);

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized(new { message = "User ID claim is missing" });

            if (!Guid.TryParse(userIdClaim, out var customerId))
                return BadRequest(new { message = "Invalid user ID format" });

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
    [Authorize]
    public async Task<IActionResult> GetOrder(Guid orderId)
    {
        try
        {
            // Support both NameIdentifier and "sub" (JWT subject) - some tokens use one or the other
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var customerId))
                return Unauthorized(new { message = "User ID claim is missing" });

            var order = await _orderService.GetOrderAsync(orderId);
            if (order == null)
                return NotFound();

            // Allow: (1) order owner (customer) or (2) vendor viewing their restaurant's order
            if (order.CustomerId == customerId)
                return Ok(order);

            _logger.LogWarning("GetOrder 403: Order {OrderId} belongs to CustomerId {OrderCustomerId}, but request has UserId {RequestUserId}. Access denied.", orderId, order.CustomerId, customerId);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order");
            return StatusCode(500, new { message = "Failed to get order" });
        }
    }

    [HttpPost("{orderId}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelOrder(Guid orderId)
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var customerId))
                return Unauthorized(new { message = "User ID claim is missing" });

            var success = await _orderService.CancelOrderByCustomerAsync(orderId, customerId);
            if (!success)
                return NotFound(new { message = "Order not found" });

            return Ok(new { message = "Order cancelled" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order {OrderId}", orderId);
            return StatusCode(500, new { message = "Failed to cancel order" });
        }
    }

    /// <summary>
    /// Internal: mark order as Refunded. Called by PaymentService after successful vendor refund.
    /// </summary>
    [HttpPatch("internal/{orderId}/refunded")]
    [AllowAnonymous]
    public async Task<IActionResult> InternalMarkOrderRefunded(Guid orderId, [FromHeader(Name = "X-Internal-Api-Key")] string? apiKey = null)
    {
        var expectedKey = _configuration["InternalApiKey"] ?? _configuration["Services:OrderService:InternalApiKey"];
        if (!string.IsNullOrEmpty(expectedKey) && apiKey != expectedKey)
        {
            _logger.LogWarning("Internal mark refunded rejected: missing or invalid API key");
            return Unauthorized(new { message = "Invalid or missing internal API key" });
        }

        try
        {
            var ok = await _orderService.UpdateOrderStatusAsync(orderId, "Refunded", "Refunded by vendor");
            if (!ok) return NotFound(new { message = "Order not found" });
            return Ok(new { message = "Order marked as refunded" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InternalMarkOrderRefunded failed for {OrderId}", orderId);
            return StatusCode(500, new { message = "Failed to mark order as refunded" });
        }
    }

    /// <summary>
    /// Internal: get order payment info (StripePaymentIntentId, Total, ServiceFee, Status). Used by PaymentService for refund fallback when PaymentIntent not in its DB.
    /// </summary>
    [HttpGet("internal/{orderId}/payment-info")]
    [AllowAnonymous]
    public async Task<IActionResult> InternalGetOrderPaymentInfo(Guid orderId, [FromHeader(Name = "X-Internal-Api-Key")] string? apiKey = null)
    {
        var expectedKey = _configuration["InternalApiKey"] ?? _configuration["Services:OrderService:InternalApiKey"];
        if (!string.IsNullOrEmpty(expectedKey) && apiKey != expectedKey)
        {
            _logger.LogWarning("Internal order payment-info rejected: missing or invalid API key");
            return Unauthorized(new { message = "Invalid or missing internal API key" });
        }

        try
        {
            var order = await _orderService.GetOrderAsync(orderId);
            if (order == null) return NotFound(new { message = "Order not found" });
            return Ok(new
            {
                orderId = order.OrderId,
                status = order.Status,
                stripePaymentIntentId = order.StripePaymentIntentId,
                total = order.Total,
                serviceFee = order.ServiceFee
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InternalGetOrderPaymentInfo failed for {OrderId}", orderId);
            return StatusCode(500, new { message = "Failed to get order payment info" });
        }
    }

    /// <summary>
    /// Internal: get order status by order id. Used by other services (e.g. PaymentService) to enforce policies.
    /// </summary>
    [HttpGet("internal/{orderId}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> InternalGetOrderStatus(Guid orderId, [FromHeader(Name = "X-Internal-Api-Key")] string? apiKey = null)
    {
        var expectedKey = _configuration["InternalApiKey"] ?? _configuration["Services:OrderService:InternalApiKey"];
        if (!string.IsNullOrEmpty(expectedKey) && apiKey != expectedKey)
        {
            _logger.LogWarning("Internal order status rejected: missing or invalid API key");
            return Unauthorized(new { message = "Invalid or missing internal API key" });
        }

        try
        {
            var order = await _orderService.GetOrderAsync(orderId);
            if (order == null) return NotFound(new { message = "Order not found" });
            return Ok(new { orderId, status = order.Status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InternalGetOrderStatus failed for {OrderId}", orderId);
            return StatusCode(500, new { message = "Failed to get order status" });
        }
    }

    /// <summary>
    /// Internal: minimal order metadata used for cross-service authorization checks (e.g. ChatService).
    /// </summary>
    [HttpGet("internal/{orderId}/metadata")]
    [AllowAnonymous]
    public async Task<IActionResult> InternalGetOrderMetadata(Guid orderId, [FromHeader(Name = "X-Internal-Api-Key")] string? apiKey = null)
    {
        var expectedKey = _configuration["InternalApiKey"] ?? _configuration["Services:OrderService:InternalApiKey"];
        if (!string.IsNullOrEmpty(expectedKey) && apiKey != expectedKey)
        {
            _logger.LogWarning("Internal order metadata rejected: missing or invalid API key");
            return Unauthorized(new { message = "Invalid or missing internal API key" });
        }

        try
        {
            var order = await _orderService.GetOrderAsync(orderId);
            if (order == null) return NotFound(new { message = "Order not found" });

            return Ok(new
            {
                orderId = order.OrderId,
                customerId = order.CustomerId,
                restaurantId = order.RestaurantId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InternalGetOrderMetadata failed for {OrderId}", orderId);
            return StatusCode(500, new { message = "Failed to get order metadata" });
        }
    }

    // ----------------------------
    // Vendor Orders (existing)
    // ----------------------------

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

    [HttpGet("vendor/restaurants/{restaurantId}/orders/{orderId}")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetVendorOrder(Guid restaurantId, Guid orderId)
    {
        try
        {
            var order = await _orderService.GetOrderAsync(orderId);
            if (order == null)
                return NotFound(new { message = "Order not found" });

            // Ensure the requested order belongs to the restaurant scope
            if (order.RestaurantId != restaurantId)
                return Forbid();

            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vendor order {OrderId} for restaurant {RestaurantId}", orderId, restaurantId);
            return StatusCode(500, new { message = "Failed to get vendor order" });
        }
    }

    [HttpGet("vendor/restaurants/{restaurantId}/orders/paged")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetVendorOrdersPaged(
        Guid restaurantId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? orderBy = null,
        [FromQuery] string? filter = null)
    {
        try
        {
            if (take <= 0) take = 20;
            if (take > 200) take = 200;

            var orders = await _orderService.GetOrdersByRestaurantAsync(restaurantId);

            // 🔹 Filtering
            if (!string.IsNullOrWhiteSpace(filter))
            {
                var f = filter.Trim();
                orders = orders
                    .Where(o =>
                        o.OrderId.ToString().Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        (o.Status?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (o.DeliveryAddress?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }

            // 🔹 Sorting (strongly typed)
            orders = ApplyOrderBy(orders, orderBy);

            var totalCount = orders.Count;
            var page = orders.Skip(skip).Take(take).ToList();

            return Ok(new PagedResult<Order>(page, totalCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vendor orders (paged)");
            return StatusCode(500, new { message = "Failed to get vendor orders (paged)" });
        }
    }


    private static List<Order> ApplyOrderBy(List<Order> orders, string? orderBy)
    {
        if (orders == null || orders.Count == 0)
            return orders;

        if (string.IsNullOrWhiteSpace(orderBy))
            return orders.OrderByDescending(o => o.CreatedAt).ToList();

        var ob = orderBy.Trim().ToLowerInvariant();
        var desc = ob.Contains(" desc");

        if (ob.Contains("createdat"))
        {
            return desc
                ? orders.OrderByDescending(o => o.CreatedAt).ToList()
                : orders.OrderBy(o => o.CreatedAt).ToList();
        }

        if (ob.Contains("total"))
        {
            return desc
                ? orders.OrderByDescending(o => o.Total).ToList()
                : orders.OrderBy(o => o.Total).ToList();
        }

        if (ob.Contains("status"))
        {
            return desc
                ? orders.OrderByDescending(o => o.Status).ToList()
                : orders.OrderBy(o => o.Status).ToList();
        }

        // Default fallback
        return orders.OrderByDescending(o => o.CreatedAt).ToList();
    }


    private static object? TryGet(dynamic obj, string propName)
    {
        try
        {
            var t = obj.GetType();
            var p = t.GetProperty(propName);
            return p?.GetValue(obj);
        }
        catch
        {
            return null;
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
                return BadRequest(new { message = "Request body is required" });

            if (string.IsNullOrWhiteSpace(request.Status))
                return BadRequest(new { message = "Status is required" });

            var order = await _orderService.GetOrderAsync(orderId);
            if (order == null)
                return NotFound(new { message = "Order not found" });

            var success = await _orderService.UpdateOrderStatusAsync(orderId, request.Status, request.Notes);
            if (!success)
                return BadRequest(new { message = "Failed to update order status" });

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

// ----------------------------
// Records (existing + new)
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
    string? SpecialInstructions,
    string? IdempotencyKey
);

public record UpdateOrderStatusRequest(
    string Status,
    string? Notes
);

// ✅ NEW helper record for grid-friendly responses
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
